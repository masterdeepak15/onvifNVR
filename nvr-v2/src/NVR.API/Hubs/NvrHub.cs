using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NVR.Core.DTOs;
using NVR.Core.Interfaces;
using NVR.Infrastructure.Data;

namespace NVR.API.Hubs
{
    /// <summary>
    /// NVR SignalR Hub — REQUIRES AUTHENTICATION (JWT via query string or header)
    ///
    /// CLIENT CONNECTS: wss://host/hubs/nvr?access_token=JWT_TOKEN
    ///
    /// Full NVR controls over WebSocket:
    /// ─ Live streaming: subscribe/unsubscribe per camera
    /// ─ Playback: play, pause, stop, seek, speed control (0.25x–8x)
    /// ─ PTZ: 8-direction move, zoom, focus, iris, presets
    /// ─ Recording: start/stop per camera
    /// ─ Events: system alerts, camera status changes
    /// ─ Analytics: live metric pushes
    ///
    /// Role-based access is enforced on every camera operation.
    /// Admins → full access
    /// Operators → Control-level by default (Record/Admin need explicit grant)
    /// Viewers → only cameras explicitly granted
    /// </summary>
    [Authorize]   // <-- ENFORCES JWT AUTH FOR ALL HUB METHODS
    public class NvrHub : Hub
    {
        private readonly IRtspStreamService _streamService;
        private readonly IOnvifService _onvif;
        private readonly ICameraAccessService _accessService;
        private readonly IAnalyticsService _analytics;
        private readonly IRecordingService _recordingService;
        private readonly NvrDbContext _db;
        private readonly ILogger<NvrHub> _logger;

        // Per-connection stream cancellation tokens: key = "{connectionId}_{cameraId}"
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _streamCts = new();

        // Playback state per connection: key = connectionId, value = PlaybackSession
        private static readonly ConcurrentDictionary<string, InMemoryPlaybackSession> _playbackSessions = new();

        // Track which cameras each connection is watching (for cleanup)
        private static readonly ConcurrentDictionary<string, ConcurrentBag<Guid>> _connectionCameras = new();

        public NvrHub(
            IRtspStreamService streamService,
            IOnvifService onvif,
            ICameraAccessService accessService,
            IAnalyticsService analytics,
            IRecordingService recordingService,
            NvrDbContext db,
            ILogger<NvrHub> logger)
        {
            _streamService = streamService;
            _onvif = onvif;
            _accessService = accessService;
            _analytics = analytics;
            _recordingService = recordingService;
            _db = db;
            _logger = logger;
        }

        // ============================================================
        // CONNECTION LIFECYCLE
        // ============================================================

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            var username = GetUsername();
            var ip = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            _logger.LogInformation("Hub connected: user={Username} connId={ConnId} ip={Ip}", username, Context.ConnectionId, ip);

            // Add to personal user group (for targeted pushes)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

            // Admin users join admin group
            if (IsAdmin())
                await Groups.AddToGroupAsync(Context.ConnectionId, "admins");

            // Init connection camera tracking
            _connectionCameras.TryAdd(Context.ConnectionId, new ConcurrentBag<Guid>());

            // Send current system state on connect
            await SendCurrentStateAsync();

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserId();
            _logger.LogInformation("Hub disconnected: user={UserId} connId={ConnId}", userId, Context.ConnectionId);

            // Cancel all streams for this connection
            if (_connectionCameras.TryRemove(Context.ConnectionId, out var cameras))
            {
                foreach (var cameraId in cameras)
                    await StopCameraStreamInternalAsync(cameraId);
            }

            // Cleanup playback session
            _playbackSessions.TryRemove(Context.ConnectionId, out _);

            // Record viewer session end in analytics
            await _analytics.RecordViewerSessionEndAsync(Context.ConnectionId);

            await base.OnDisconnectedAsync(exception);
        }

        // ============================================================
        // LIVE STREAM SUBSCRIBE / UNSUBSCRIBE
        // ============================================================

        /// <summary>Start receiving live JPEG frames for a camera</summary>
        public async Task SubscribeToCamera(Guid cameraId)
        {
            if (!await AssertPermissionAsync(cameraId, CameraPermissions.View)) return;

            var userId = GetUserId();
            var ip = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            _logger.LogInformation("Live subscribe: user={UserId} camera={CameraId}", userId, cameraId);

            await Groups.AddToGroupAsync(Context.ConnectionId, $"camera_{cameraId}");

            // Track this connection's camera
            if (_connectionCameras.TryGetValue(Context.ConnectionId, out var camBag))
                camBag.Add(cameraId);

            // Record analytics viewer session
            await _analytics.RecordViewerSessionStartAsync(cameraId, userId, Context.ConnectionId, ip);

            // Start frame push loop for this client
            var cts = new CancellationTokenSource();
            var key = $"{Context.ConnectionId}_{cameraId}";
            _streamCts.TryAdd(key, cts);

            _ = Task.Run(() => PushLiveFramesAsync(cameraId, cts.Token), cts.Token);

            await Clients.Caller.SendAsync("StreamState", new StreamStateDto
            {
                CameraId = cameraId,
                State = "Live",
                IsLive = true,
                Speed = 1.0f
            });
        }

        /// <summary>Stop receiving frames for a camera</summary>
        public async Task UnsubscribeFromCamera(Guid cameraId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"camera_{cameraId}");
            await StopCameraStreamInternalAsync(cameraId);
            await _analytics.RecordViewerSessionEndAsync(Context.ConnectionId);
        }

        /// <summary>Subscribe to system-wide events (alerts, status changes, analytics)</summary>
        public async Task SubscribeToEvents()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "events");
            _logger.LogDebug("User {UserId} subscribed to events", GetUserId());
        }

        // ============================================================
        // STREAM CONTROLS (Play/Pause/Stop/Speed/Seek)
        // ============================================================

        /// <summary>
        /// Universal NVR stream control command.
        /// Commands: Play, Pause, Resume, Stop, SetSpeed, Seek, ZoomIn, ZoomOut, ZoomReset
        /// </summary>
        public async Task StreamControl(StreamControlCommand cmd)
        {
            if (!await AssertPermissionAsync(cmd.CameraId, CameraPermissions.View)) return;

            _logger.LogDebug("StreamControl: cmd={Command} camera={CameraId} user={UserId}", cmd.Command, cmd.CameraId, GetUserId());

            switch (cmd.Command.ToLower())
            {
                case "play":
                    await HandlePlayAsync(cmd);
                    break;

                case "pause":
                    await HandlePauseAsync(cmd.CameraId);
                    break;

                case "resume":
                    await HandleResumeAsync(cmd.CameraId);
                    break;

                case "stop":
                    await HandleStopAsync(cmd.CameraId);
                    break;

                case "setspeed":
                    await HandleSetSpeedAsync(cmd.CameraId, cmd.Speed ?? 1.0f);
                    break;

                case "seek":
                    if (cmd.SeekTo.HasValue)
                        await HandleSeekAsync(cmd.CameraId, cmd.SeekTo.Value);
                    break;

                case "zoomin":
                    await HandleDigitalZoomAsync(cmd.CameraId, 0.1f);
                    break;

                case "zoomout":
                    await HandleDigitalZoomAsync(cmd.CameraId, -0.1f);
                    break;

                case "zoomreset":
                    await HandleDigitalZoomAsync(cmd.CameraId, 0f, reset: true);
                    break;

                case "golive":
                    await HandleGoLiveAsync(cmd.CameraId);
                    break;

                default:
                    await Clients.Caller.SendAsync("Error", new { Message = $"Unknown stream command: {cmd.Command}" });
                    break;
            }
        }

        private async Task HandlePlayAsync(StreamControlCommand cmd)
        {
            // If SeekTo is provided → playback from recorded position
            // If no SeekTo → live stream
            if (cmd.SeekTo.HasValue)
            {
                var session = GetOrCreatePlaybackSession(cmd.CameraId);
                session.Position = cmd.SeekTo.Value;
                session.Speed = cmd.Speed ?? 1.0f;
                session.State = "Playing";

                // Cancel live stream and start playback stream
                await StopCameraStreamInternalAsync(cmd.CameraId);
                var cts = new CancellationTokenSource();
                _streamCts.TryAdd($"{Context.ConnectionId}_{cmd.CameraId}", cts);
                _ = Task.Run(() => PushPlaybackFramesAsync(cmd.CameraId, session, cts.Token), cts.Token);

                await Clients.Caller.SendAsync("StreamState", new StreamStateDto
                {
                    CameraId = cmd.CameraId,
                    State = "Playing",
                    IsLive = false,
                    Speed = session.Speed,
                    PlaybackPosition = session.Position
                });
            }
            else
            {
                await HandleGoLiveAsync(cmd.CameraId);
            }
        }

        private async Task HandlePauseAsync(Guid cameraId)
        {
            var key = $"{Context.ConnectionId}_{cameraId}";
            if (_streamCts.TryGetValue(key, out var cts))
            {
                cts.Cancel(); // Pause = stop frame push (client keeps last frame)
                _streamCts.TryRemove(key, out _);
            }

            if (_playbackSessions.TryGetValue(Context.ConnectionId, out var session))
                session.State = "Paused";

            await Clients.Caller.SendAsync("StreamState", new StreamStateDto
            {
                CameraId = cameraId,
                State = "Paused",
                IsLive = false
            });
        }

        private async Task HandleResumeAsync(Guid cameraId)
        {
            if (_playbackSessions.TryGetValue(Context.ConnectionId, out var session) && session.State == "Paused")
            {
                session.State = "Playing";
                var cts = new CancellationTokenSource();
                _streamCts.TryAdd($"{Context.ConnectionId}_{cameraId}", cts);
                _ = Task.Run(() => PushPlaybackFramesAsync(cameraId, session, cts.Token), cts.Token);

                await Clients.Caller.SendAsync("StreamState", new StreamStateDto
                {
                    CameraId = cameraId,
                    State = "Playing",
                    IsLive = false,
                    Speed = session.Speed,
                    PlaybackPosition = session.Position
                });
            }
        }

        private async Task HandleStopAsync(Guid cameraId)
        {
            await StopCameraStreamInternalAsync(cameraId);
            _playbackSessions.TryRemove(Context.ConnectionId, out _);

            await Clients.Caller.SendAsync("StreamState", new StreamStateDto
            {
                CameraId = cameraId,
                State = "Stopped",
                IsLive = false
            });
        }

        private async Task HandleSetSpeedAsync(Guid cameraId, float speed)
        {
            // Valid speeds: 0.25, 0.5, 1.0, 2.0, 4.0, 8.0
            speed = Math.Clamp(speed, 0.1f, 16.0f);

            if (_playbackSessions.TryGetValue(Context.ConnectionId, out var session))
            {
                session.Speed = speed;
                await Clients.Caller.SendAsync("StreamState", new StreamStateDto
                {
                    CameraId = cameraId,
                    State = session.State,
                    IsLive = false,
                    Speed = speed,
                    PlaybackPosition = session.Position
                });
            }
        }

        private async Task HandleSeekAsync(Guid cameraId, DateTime seekTo)
        {
            var session = GetOrCreatePlaybackSession(cameraId);
            session.Position = seekTo;

            // Restart playback from new position
            await StopCameraStreamInternalAsync(cameraId);
            var cts = new CancellationTokenSource();
            _streamCts.TryAdd($"{Context.ConnectionId}_{cameraId}", cts);
            session.State = "Playing";
            _ = Task.Run(() => PushPlaybackFramesAsync(cameraId, session, cts.Token), cts.Token);

            await Clients.Caller.SendAsync("StreamState", new StreamStateDto
            {
                CameraId = cameraId,
                State = "Playing",
                IsLive = false,
                Speed = session.Speed,
                PlaybackPosition = seekTo
            });

            await Clients.Caller.SendAsync("SeekComplete", new { CameraId = cameraId, Position = seekTo });
        }

        private async Task HandleDigitalZoomAsync(Guid cameraId, float delta, bool reset = false)
        {
            // Digital zoom is applied client-side via CSS transform
            // Server just broadcasts the zoom level change
            var key = $"zoom_{Context.ConnectionId}_{cameraId}";
            // (In production you'd store zoom state server-side or pass delta to client)
            await Clients.Caller.SendAsync("ZoomChanged", new
            {
                CameraId = cameraId,
                ZoomDelta = reset ? 0f : delta,
                Reset = reset
            });
        }

        private async Task HandleGoLiveAsync(Guid cameraId)
        {
            // Switch from playback back to live stream
            _playbackSessions.TryRemove(Context.ConnectionId, out _);
            await StopCameraStreamInternalAsync(cameraId);

            var cts = new CancellationTokenSource();
            _streamCts.TryAdd($"{Context.ConnectionId}_{cameraId}", cts);
            _ = Task.Run(() => PushLiveFramesAsync(cameraId, cts.Token), cts.Token);

            await Clients.Caller.SendAsync("StreamState", new StreamStateDto
            {
                CameraId = cameraId,
                State = "Live",
                IsLive = true,
                Speed = 1.0f
            });
        }

        // ============================================================
        // RECORDING CONTROL (requires Record permission)
        // ============================================================

        public async Task StartRecording(Guid cameraId)
        {
            if (!await AssertPermissionAsync(cameraId, CameraPermissions.Record)) return;

            try
            {
                await _recordingService.StartRecordingAsync(cameraId);
                await Clients.Caller.SendAsync("RecordingStatus", new RecordingStatusPayload
                {
                    CameraId = cameraId,
                    Status = "Started",
                    Timestamp = DateTime.UtcNow
                });
                // Notify all viewers of this camera
                await Clients.Group($"camera_{cameraId}").SendAsync("CameraStatus", new CameraStatusPayload
                {
                    CameraId = cameraId,
                    IsRecording = true,
                    IsOnline = true
                });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", new { CameraId = cameraId, Message = ex.Message });
            }
        }

        public async Task StopRecording(Guid cameraId)
        {
            if (!await AssertPermissionAsync(cameraId, CameraPermissions.Record)) return;

            await _recordingService.StopRecordingAsync(cameraId);
            await Clients.Caller.SendAsync("RecordingStatus", new RecordingStatusPayload
            {
                CameraId = cameraId,
                Status = "Stopped",
                Timestamp = DateTime.UtcNow
            });
        }


        // ============================================================
        // PTZ CONTROL (requires Control permission)
        // ============================================================

        /// <summary>
        /// Full PTZ command handler.
        /// Actions: MoveUp, MoveDown, MoveLeft, MoveRight, MoveUpLeft, MoveUpRight,
        ///          MoveDownLeft, MoveDownRight, ZoomIn, ZoomOut, Stop, Home,
        ///          GoToPreset, SavePreset, DeletePreset,
        ///          FocusNear, FocusFar, FocusAuto, IrisOpen, IrisClose, IrisAuto,
        ///          AbsoluteMove, RelativeMove, ContinuousMove
        /// </summary>
        public async Task PtzCommand(PtzCommandDto cmd)
        {
            if (!await AssertPermissionAsync(cmd.CameraId, CameraPermissions.Control)) return;

            var camera = await _db.Cameras.FindAsync(cmd.CameraId);
            if (camera == null)
            {
                await Clients.Caller.SendAsync("Error", new { Message = "Camera not found" });
                return;
            }

            if (!camera.PtzCapable && !IsPtzOptional(cmd.Action))
            {
                await Clients.Caller.SendAsync("PtzFeedback", new PtzFeedbackPayload
                {
                    CameraId = cmd.CameraId,
                    Action = cmd.Action,
                    Success = false,
                    Error = "This camera does not support PTZ"
                });
                return;
            }

            try
            {
                await ExecutePtzActionAsync(camera, cmd);

                var feedback = new PtzFeedbackPayload
                {
                    CameraId = cmd.CameraId,
                    Action = cmd.Action,
                    Success = true
                };

                var actionLower = cmd.Action.ToLower();
                bool isFocusAction = actionLower.StartsWith("focus");
                bool isIrisAction = actionLower.StartsWith("iris");

                if (isFocusAction)
                {
                    // Return focus state from Imaging Service
                    try
                    {
                        var focusStatus = await _onvif.GetFocusStatusAsync(camera);
                        feedback.FocusPosition = focusStatus.Position;
                        feedback.FocusMode = focusStatus.Mode.ToString();
                        feedback.FocusMoveStatus = focusStatus.MoveStatus;
                    }
                    catch { /* non-fatal: feedback still sent with Success=true */ }
                }
                else if (isIrisAction)
                {
                    // Return iris/exposure state from Imaging Service
                    try
                    {
                        var irisStatus = await _onvif.GetIrisStatusAsync(camera);
                        feedback.IrisLevel = irisStatus.Level;
                        feedback.IrisMode = irisStatus.Mode.ToString();
                    }
                    catch { /* non-fatal */ }
                }
                else
                {
                    // PTZ action — return pan/tilt/zoom position
                    var ptzStatus = await _onvif.GetPtzStatusAsync(camera);
                    feedback.Pan = ptzStatus.Pan;
                    feedback.Tilt = ptzStatus.Tilt;
                    feedback.Zoom = ptzStatus.Zoom;
                    feedback.MoveStatus = ptzStatus.MoveStatus;
                }

                await Clients.Caller.SendAsync("PtzFeedback", feedback);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PTZ command failed: {Action} camera={CameraId}", cmd.Action, cmd.CameraId);
                await Clients.Caller.SendAsync("PtzFeedback", new PtzFeedbackPayload
                {
                    CameraId = cmd.CameraId,
                    Action = cmd.Action,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        private async Task ExecutePtzActionAsync(Core.Entities.Camera camera, PtzCommandDto cmd)
        {
            switch (cmd.Action.ToLower())
            {
                // ---- Directional moves (continuous) ----
                case "moveup":
                    await _onvif.PtzContinuousMoveAsync(camera, 0, cmd.Speed, 0); break;
                case "movedown":
                    await _onvif.PtzContinuousMoveAsync(camera, 0, -cmd.Speed, 0); break;
                case "moveleft":
                    await _onvif.PtzContinuousMoveAsync(camera, -cmd.Speed, 0, 0); break;
                case "moveright":
                    await _onvif.PtzContinuousMoveAsync(camera, cmd.Speed, 0, 0); break;
                case "moveupleft":
                    await _onvif.PtzContinuousMoveAsync(camera, -cmd.Speed, cmd.Speed, 0); break;
                case "moveupright":
                    await _onvif.PtzContinuousMoveAsync(camera, cmd.Speed, cmd.Speed, 0); break;
                case "movedownleft":
                    await _onvif.PtzContinuousMoveAsync(camera, -cmd.Speed, -cmd.Speed, 0); break;
                case "movedownright":
                    await _onvif.PtzContinuousMoveAsync(camera, cmd.Speed, -cmd.Speed, 0); break;

                // ---- Zoom ----
                case "zoomin":
                    await _onvif.PtzContinuousMoveAsync(camera, 0, 0, cmd.Speed); break;
                case "zoomout":
                    await _onvif.PtzContinuousMoveAsync(camera, 0, 0, -cmd.Speed); break;

                // ---- Stop all movement ----
                case "stop":
                    await _onvif.PtzStopAsync(camera); break;

                // ---- Home position ----
                case "home":
                    await _onvif.PtzAbsoluteMoveAsync(camera, 0, 0, 0); break;

                // ---- Absolute / Relative / Continuous (raw values) ----
                case "absolutemove":
                    await _onvif.PtzAbsoluteMoveAsync(camera, cmd.Pan, cmd.Tilt, cmd.Zoom); break;
                case "relativemove":
                    await _onvif.PtzRelativeMoveAsync(camera, cmd.Pan, cmd.Tilt, cmd.Zoom); break;
                case "continuousmove":
                    await _onvif.PtzContinuousMoveAsync(camera, cmd.Pan, cmd.Tilt, cmd.Zoom); break;

                // ---- Presets ----
                case "gotopreset":
                    if (!string.IsNullOrEmpty(cmd.PresetToken))
                        await _onvif.GoToPresetAsync(camera, cmd.PresetToken);
                    break;
                case "savepreset":
                    if (!string.IsNullOrEmpty(cmd.PresetName))
                    {
                        var token = await _onvif.SetPresetAsync(camera, cmd.PresetName);
                        var status = await _onvif.GetPtzStatusAsync(camera);
                        var preset = new Core.Entities.PtzPreset
                        {
                            CameraId = camera.Id,
                            Name = cmd.PresetName,
                            OnvifToken = token,
                            PanPosition = status.Pan,
                            TiltPosition = status.Tilt,
                            ZoomPosition = status.Zoom
                        };
                        _db.PtzPresets.Add(preset);
                        await _db.SaveChangesAsync();
                        // Notify caller of new preset
                        await Clients.Caller.SendAsync("PresetSaved", new PtzPresetDto
                        {
                            Id = preset.Id,
                            Name = preset.Name,
                            OnvifToken = token,
                            PanPosition = status.Pan,
                            TiltPosition = status.Tilt,
                            ZoomPosition = status.Zoom
                        });
                    }
                    break;
                case "deletepreset":
                    if (!string.IsNullOrEmpty(cmd.PresetToken))
                    {
                        await _onvif.RemovePresetAsync(camera, cmd.PresetToken);
                        var dbPreset = await _db.PtzPresets.FirstOrDefaultAsync(p => p.CameraId == camera.Id && p.OnvifToken == cmd.PresetToken);
                        if (dbPreset != null) { _db.PtzPresets.Remove(dbPreset); await _db.SaveChangesAsync(); }
                    }
                    break;

                // ---- Focus ----
                // Uses ONVIF Imaging Service (http://<camera-ip>/onvif/imaging)
                // timg:Move for continuous/absolute moves; timg:SetImagingSettings for mode
                case "focusnear":
                    // Continuous move toward near limit. Stops when FocusStop is called.
                    await _onvif.FocusContinuousMoveAsync(camera, -cmd.Speed); break;

                case "focusfar":
                    // Continuous move toward far limit. Stops when FocusStop is called.
                    await _onvif.FocusContinuousMoveAsync(camera, cmd.Speed); break;

                case "focusstop":
                    // Stop any in-progress continuous focus movement.
                    await _onvif.FocusContinuousMoveAsync(camera, 0f); break;

                case "focusabsolute":
                    // Move to absolute focus position. Uses cmd.Zoom field as position (0.0 = near, 1.0 = far).
                    await _onvif.FocusAbsoluteMoveAsync(camera, cmd.Zoom, cmd.Speed); break;

                case "focusauto":
                    // Enable camera auto-focus (AutoFocusMode = AUTO).
                    await _onvif.SetFocusModeAsync(camera, NVR.Core.Interfaces.FocusMode.Auto); break;

                case "focusmanual":
                    // Disable auto-focus and lock to current position.
                    await _onvif.SetFocusModeAsync(camera, NVR.Core.Interfaces.FocusMode.Manual); break;

                // ---- Iris / Exposure ----
                // Uses ONVIF Imaging Service SetImagingSettings with Exposure block.
                // IrisOpen/IrisClose set Exposure Mode to MANUAL with max/min iris value.
                // IrisAuto/IrisManual toggle the Exposure.Mode field.
                case "irisopen":
                    // Force MANUAL exposure mode and set iris fully open (level = 1.0).
                    await _onvif.SetIrisAsync(camera, 1.0f); break;

                case "irisclose":
                    // Force MANUAL exposure mode and set iris fully closed (level = 0.0).
                    await _onvif.SetIrisAsync(camera, 0.0f); break;

                case "irisset":
                    // Set iris to specific level. Uses cmd.Zoom field as level (0.0 closed, 1.0 open).
                    await _onvif.SetIrisAsync(camera, cmd.Zoom); break;

                case "irisauto":
                    // Enable auto-iris (Exposure.Mode = AUTO). Camera manages exposure automatically.
                    await _onvif.SetIrisModeAsync(camera, NVR.Core.Interfaces.IrisMode.Auto); break;

                case "irismanual":
                    // Switch to manual iris control. Use IrisSet to set specific level after this.
                    await _onvif.SetIrisModeAsync(camera, NVR.Core.Interfaces.IrisMode.Manual); break;
            }
        }

        private bool IsPtzOptional(string action)
        {
            // Digital zoom actions work on any camera (client CSS transform — no hardware PTZ needed).
            // Focus and iris actions need the Imaging Service but NOT the PTZ Service,
            // so they can proceed even when ptzCapable = false.
            return action.ToLower() is
                "zoomin" or "zoomout" or "zoomreset" or
                "focusnear" or "focusfar" or "focusstop" or "focusabsolute" or "focusauto" or "focusmanual" or
                "irisopen" or "irisclose" or "irisset" or "irisauto" or "irismanual";
        }
        // ============================================================
        // SNAPSHOT REQUEST
        // ============================================================

        public async Task RequestSnapshot(Guid cameraId)
        {
            if (!await AssertPermissionAsync(cameraId, CameraPermissions.View)) return;

            try
            {
                var camera = await _db.Cameras.FindAsync(cameraId);
                if (camera == null) return;

                // Try live frame first (faster)
                var frame = await _streamService.GetLatestFrameAsync(cameraId);
                if (frame.Length == 0)
                    frame = await _onvif.GetSnapshotAsync(camera);

                if (frame.Length > 0)
                {
                    await Clients.Caller.SendAsync("Snapshot", new
                    {
                        CameraId = cameraId,
                        Frame = Convert.ToBase64String(frame),
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("Error", new { CameraId = cameraId, Message = "Snapshot failed: " + ex.Message });
            }
        }

        // ============================================================
        // ALERT ACKNOWLEDGEMENT
        // ============================================================

        public async Task AcknowledgeAlert(Guid alertId)
        {
            var alert = await _db.CameraEvents.FindAsync(alertId);
            if (alert == null) return;

            if (!await AssertPermissionAsync(alert.CameraId, CameraPermissions.View)) return;

            alert.IsAcknowledged = true;
            alert.AcknowledgedAt = DateTime.UtcNow;
            alert.AcknowledgedBy = GetUsername();
            await _db.SaveChangesAsync();

            await Clients.Group("events").SendAsync("AlertAcknowledged", new { AlertId = alertId, AcknowledgedBy = GetUsername() });
        }

        // ============================================================
        // GET CURRENT PTZ STATUS
        // ============================================================

        public async Task GetPtzStatus(Guid cameraId)
        {
            if (!await AssertPermissionAsync(cameraId, CameraPermissions.View)) return;

            var camera = await _db.Cameras.Include(c => c.PtzPresets).FirstOrDefaultAsync(c => c.Id == cameraId);
            if (camera == null) return;

            if (!camera.PtzCapable)
            {
                await Clients.Caller.SendAsync("PtzStatus", new PtzStatusDto
                {
                    CameraId = cameraId,
                    MoveStatus = "NotSupported"
                });
                return;
            }

            try
            {
                var status = await _onvif.GetPtzStatusAsync(camera);
                var presets = camera.PtzPresets.Select(p => new PtzPresetDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    OnvifToken = p.OnvifToken,
                    PanPosition = p.PanPosition,
                    TiltPosition = p.TiltPosition,
                    ZoomPosition = p.ZoomPosition
                }).ToList();

                await Clients.Caller.SendAsync("PtzStatus", new PtzStatusDto
                {
                    CameraId = cameraId,
                    Pan = status.Pan,
                    Tilt = status.Tilt,
                    Zoom = status.Zoom,
                    MoveStatus = status.MoveStatus,
                    Presets = presets
                });
            }
            catch (Exception ex)
            {
                await Clients.Caller.SendAsync("PtzStatus", new PtzStatusDto
                {
                    CameraId = cameraId,
                    MoveStatus = "Error"
                });
            }
        }

        // ============================================================
        // INTERNAL: FRAME PUSH LOOPS
        // ============================================================

        private async Task PushLiveFramesAsync(Guid cameraId, CancellationToken ct)
        {
            try
            {
                await _streamService.StartStreamAsync(cameraId, ct);
                int fps = 0;
                var fpsTimer = DateTime.UtcNow;

                await foreach (var frame in _streamService.GetFrameStreamAsync(cameraId, ct))
                {
                    fps++;
                    var elapsed = (DateTime.UtcNow - fpsTimer).TotalSeconds;
                    int actualFps = elapsed > 0 ? (int)(fps / elapsed) : 0;

                    if (elapsed >= 1.0) { fps = 0; fpsTimer = DateTime.UtcNow; }

                    await Clients.Caller.SendAsync("CameraFrame", new CameraFramePayload
                    {
                        CameraId = cameraId,
                        Frame = Convert.ToBase64String(frame),
                        TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        StreamState = "Live",
                        Fps = actualFps
                    }, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Live stream error for camera {CameraId}", cameraId);
                await Clients.Caller.SendAsync("StreamError", new
                {
                    CameraId = cameraId,
                    Error = "Stream interrupted: " + ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        private async Task PushPlaybackFramesAsync(Guid cameraId, InMemoryPlaybackSession session, CancellationToken ct)
        {
            try
            {
                // Find recording chunks for the requested time
                var chunks = await _db.RecordingChunks
                    .Where(c => c.CameraId == cameraId && c.StartTime >= session.Position)
                    .OrderBy(c => c.StartTime)
                    .Take(100)
                    .ToListAsync(ct);

                if (!chunks.Any())
                {
                    await Clients.Caller.SendAsync("StreamState", new StreamStateDto
                    {
                        CameraId = cameraId,
                        State = "NoRecording",
                        IsLive = false,
                        PlaybackPosition = session.Position
                    });
                    return;
                }

                // For each chunk, we'd transcode and push frames
                // In a production system: use FFmpeg to decode MPEG-TS and extract frames
                // Here we signal playback start and client fetches chunks via HLS-compatible endpoint
                await Clients.Caller.SendAsync("PlaybackReady", new
                {
                    CameraId = cameraId,
                    StartPosition = session.Position,
                    ChunkCount = chunks.Count,
                    FirstChunkPath = chunks.First().FilePath,
                    Speed = session.Speed
                });

                // Simulate pushing position updates
                var pos = session.Position;
                while (session.State == "Playing" && !ct.IsCancellationRequested)
                {
                    await Task.Delay((int)(1000 / session.Speed), ct);
                    pos = pos.AddSeconds(1 * session.Speed);
                    session.Position = pos;

                    await Clients.Caller.SendAsync("PlaybackPosition", new
                    {
                        CameraId = cameraId,
                        Position = pos,
                        Speed = session.Speed
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Playback error for camera {CameraId}", cameraId);
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private async Task StopCameraStreamInternalAsync(Guid cameraId)
        {
            var key = $"{Context.ConnectionId}_{cameraId}";
            if (_streamCts.TryRemove(key, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        private InMemoryPlaybackSession GetOrCreatePlaybackSession(Guid cameraId)
        {
            if (!_playbackSessions.TryGetValue(Context.ConnectionId, out var session))
            {
                session = new InMemoryPlaybackSession { CameraId = cameraId };
                _playbackSessions.TryAdd(Context.ConnectionId, session);
            }
            return session;
        }

        private async Task SendCurrentStateAsync()
        {
            var cameras = await _db.Cameras.Select(c => new
            {
                c.Id,
                c.Name,
                c.Status,
                c.IsOnline,
                c.IsRecording,
                c.LastSeenAt
            }).ToListAsync();

            await Clients.Caller.SendAsync("SystemState", new
            {
                Cameras = cameras,
                ServerTime = DateTime.UtcNow
            });
        }

        private async Task<bool> AssertPermissionAsync(Guid cameraId, string requiredPermission)
        {
            var userId = GetUserId();
            var hasAccess = await _accessService.HasPermissionAsync(userId, cameraId, requiredPermission);
            if (!hasAccess)
            {
                await Clients.Caller.SendAsync("AccessDenied", new
                {
                    CameraId = cameraId,
                    RequiredPermission = requiredPermission,
                    Message = $"You don't have '{requiredPermission}' access to this camera"
                });
                _logger.LogWarning("Access denied: user={UserId} camera={CameraId} required={Permission}",
                    userId, cameraId, requiredPermission);
            }
            return hasAccess;
        }

        private string GetUserId() =>
            Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        private string GetUsername() =>
            Context.User?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

        private string GetUserRole() =>
            Context.User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

        private bool IsAdmin() =>
            GetUserRole() == "Admin";
    }

    // ============================================================
    // IN-MEMORY PLAYBACK STATE
    // ============================================================
    internal class InMemoryPlaybackSession
    {
        public Guid CameraId { get; set; }
        public DateTime Position { get; set; } = DateTime.UtcNow;
        public float Speed { get; set; } = 1.0f;
        public string State { get; set; } = "Paused"; // Playing | Paused | Stopped
    }

    // ============================================================
    // SERVER → CLIENT EMITTER (used by background services / API)
    // ============================================================
    public class NvrHubEventEmitter : INvrHubEventEmitter
    {
        private readonly IHubContext<NvrHub> _hubContext;

        public NvrHubEventEmitter(IHubContext<NvrHub> hubContext) => _hubContext = hubContext;

        public Task SendCameraStatusAsync(Guid cameraId, string status, bool isOnline, bool isRecording, int viewers = 0)
            => _hubContext.Clients.Group("events").SendAsync("CameraStatus", new CameraStatusPayload
            {
                CameraId = cameraId,
                Status = status,
                IsOnline = isOnline,
                IsRecording = isRecording,
                LastSeenAt = DateTime.UtcNow,
                ActiveViewers = viewers
            });

        public Task SendAlertAsync(Guid? cameraId, string cameraName, string alertType, string message, string severity = "Warning", string? snapshotBase64 = null)
            => _hubContext.Clients.Group("events").SendAsync("Alert", new AlertPayload
            {
                AlertId = Guid.NewGuid(),
                CameraId = cameraId,
                CameraName = cameraName,
                Type = alertType,
                Message = message,
                Severity = severity,
                Timestamp = DateTime.UtcNow,
                SnapshotBase64 = snapshotBase64
            });

        public Task SendRecordingStatusAsync(Guid cameraId, Guid? recordingId, string status, int? chunk = null, long? sizeBytes = null)
            => _hubContext.Clients.Group($"camera_{cameraId}").SendAsync("RecordingStatus", new RecordingStatusPayload
            {
                CameraId = cameraId,
                RecordingId = recordingId,
                Status = status,
                ChunkNumber = chunk,
                FileSizeBytes = sizeBytes,
                Timestamp = DateTime.UtcNow
            });

        public Task SendStorageAlertAsync(Guid storageProfileId, double usagePercent, string message)
            => _hubContext.Clients.Group("events").SendAsync("StorageAlert", new
            {
                StorageProfileId = storageProfileId,
                UsagePercent = usagePercent,
                Message = message,
                Severity = usagePercent > 95 ? "Critical" : "Warning",
                Timestamp = DateTime.UtcNow
            });

        public Task SendAnalyticsUpdateAsync(int online, int recording, int viewers, double storagePercent, int unackedAlerts)
            => _hubContext.Clients.Group("events").SendAsync("AnalyticsUpdate", new AnalyticsUpdatePayload
            {
                OnlineCameras = online,
                RecordingCameras = recording,
                ActiveViewers = viewers,
                StorageUsagePercent = storagePercent,
                UnacknowledgedAlerts = unackedAlerts
            });

        /// <summary>Push alert to a specific user's group only</summary>
        public Task SendUserAlertAsync(string userId, string message, string severity = "Info")
            => _hubContext.Clients.Group($"user_{userId}").SendAsync("UserNotification", new
            {
                Message = message,
                Severity = severity,
                Timestamp = DateTime.UtcNow
            });
    }

}
