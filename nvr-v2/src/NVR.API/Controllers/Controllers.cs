using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NVR.Core.DTOs;
using NVR.Core.Entities;
using NVR.Core.Interfaces;
using NVR.Infrastructure.Data;
using NVR.Infrastructure.Services;

namespace NVR.API.Controllers
{
    // ============================================================
    // AUTH CONTROLLER
    // ============================================================
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        public AuthController(IAuthService authService) => _authService = authService;

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            try { return Ok(await _authService.LoginAsync(request)); }
            catch (UnauthorizedAccessException) { return Unauthorized(new { message = "Invalid credentials" }); }
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshTokenRequest request)
        {
            try { return Ok(await _authService.RefreshTokenAsync(request.RefreshToken)); }
            catch (UnauthorizedAccessException) { return Unauthorized(new { message = "Invalid refresh token" }); }
        }

        [HttpPost("register"), Authorize(Roles = "Admin")]
        public async Task<ActionResult<UserDto>> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var user = await _authService.RegisterAsync(request);
                return Ok(new UserDto { Id = user.Id, Username = user.Username, Email = user.Email, Role = user.Role });
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpPost("logout"), Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _authService.LogoutAsync(userId);
            return NoContent();
        }

        [HttpGet("me"), Authorize]
        public async Task<ActionResult<UserDto>> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null) return NotFound();
            return Ok(new UserDto { Id = user.Id, Username = user.Username, Email = user.Email, Role = user.Role, LastLoginAt = user.LastLoginAt });
        }
    }

    // ============================================================
    // USERS CONTROLLER
    // ============================================================
    [ApiController, Route("api/users"), Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly IAuthService _authService;
        public UsersController(IAuthService authService) => _authService = authService;

        [HttpGet]
        public async Task<ActionResult<List<UserDto>>> GetAll()
        {
            var users = await _authService.GetAllUsersAsync();
            return Ok(users.Select(u => new UserDto { Id = u.Id, Username = u.Username, Email = u.Email, Role = u.Role, LastLoginAt = u.LastLoginAt }));
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<UserDto>> Update(string id, [FromBody] UpdateUserRequest request)
        {
            var user = await _authService.UpdateUserAsync(id, request);
            return Ok(new UserDto { Id = user.Id, Username = user.Username, Email = user.Email, Role = user.Role });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            await _authService.DeleteUserAsync(id);
            return NoContent();
        }
    }

    // ============================================================
    // CAMERAS CONTROLLER
    // ============================================================
    [ApiController, Route("api/cameras"), Authorize]
    public class CamerasController : ControllerBase
    {
        private readonly NvrDbContext _db;
        private readonly IOnvifService _onvif;
        private readonly IRecordingService _recording;

        public CamerasController(NvrDbContext db, IOnvifService onvif, IRecordingService recording)
        {
            _db = db; _onvif = onvif; _recording = recording;
        }

        [HttpGet]
        public async Task<ActionResult<List<CameraDto>>> GetAll()
        {
            var cameras = await _db.Cameras
                .Include(c => c.PtzPresets)
                .OrderBy(c => c.Name)
                .ToListAsync();
            return Ok(cameras.Select(MapToDto));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CameraDto>> Get(Guid id)
        {
            var camera = await _db.Cameras.Include(c => c.PtzPresets).FirstOrDefaultAsync(c => c.Id == id);
            if (camera == null) return NotFound();
            return Ok(MapToDto(camera));
        }

        [HttpPost, Authorize(Roles = "Admin,Operator")]
        public async Task<ActionResult<CameraDto>> Add([FromBody] AddCameraRequest request)
        {
            var camera = new Camera
            {
                Name = request.Name,
                IpAddress = request.IpAddress,
                Port = request.Port,
                Username = request.Username,
                Password = request.Password,
                StorageProfileId = request.StorageProfileId,
                OnvifServiceUrl = $"http://{request.IpAddress}:{request.Port}/onvif/device_service"
            };

            // Auto-discover ONVIF capabilities
            if (request.AutoDiscover)
            {
                try
                {
                    var deviceInfo = await _onvif.GetDeviceInfoAsync(camera);
                    camera.Manufacturer = deviceInfo.Manufacturer;
                    camera.Model = deviceInfo.Model;
                    camera.FirmwareVersion = deviceInfo.FirmwareVersion;
                    camera.SerialNumber = deviceInfo.SerialNumber;

                    // Get main stream RTSP URL
                    var profiles = (await _onvif.GetProfilesAsync(camera)).ToList();
                    if (profiles.Any())
                    {
                        var mainProfile = profiles.First();
                        camera.RtspUrl = await _onvif.GetRtspStreamUriAsync(camera, mainProfile.Token);
                        camera.Resolution_Width = mainProfile.Width;
                        camera.Resolution_Height = mainProfile.Height;
                        camera.Framerate = mainProfile.Framerate;
                        camera.Codec = mainProfile.VideoEncoding;
                    }

                    // Check PTZ capability
                    try { await _onvif.GetPtzStatusAsync(camera); camera.PtzCapable = true; }
                    catch { camera.PtzCapable = false; }

                    camera.IsOnline = true;
                    camera.Status = "Online";
                    camera.LastSeenAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    // Fallback to manual RTSP if provided
                    if (!string.IsNullOrEmpty(request.RtspUrl))
                        camera.RtspUrl = request.RtspUrl;
                    camera.Status = "Error";
                    camera.LastError = ex.Message;
                }
            }
            else if (!string.IsNullOrEmpty(request.RtspUrl))
            {
                camera.RtspUrl = request.RtspUrl;
            }

            _db.Cameras.Add(camera);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = camera.Id }, MapToDto(camera));
        }

        [HttpPut("{id}"), Authorize(Roles = "Admin,Operator")]
        public async Task<ActionResult<CameraDto>> Update(Guid id, [FromBody] UpdateCameraRequest request)
        {
            var camera = await _db.Cameras.FindAsync(id);
            if (camera == null) return NotFound();

            if (request.Name != null) camera.Name = request.Name;
            if (request.Username != null) camera.Username = request.Username;
            if (request.Password != null) camera.Password = request.Password;
            if (request.RtspUrl != null) camera.RtspUrl = request.RtspUrl;
            if (request.StorageProfileId.HasValue) camera.StorageProfileId = request.StorageProfileId;
            if (request.AudioEnabled.HasValue) camera.AudioEnabled = request.AudioEnabled.Value;

            await _db.SaveChangesAsync();
            return Ok(MapToDto(camera));
        }

        [HttpDelete("{id}"), Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var camera = await _db.Cameras.FindAsync(id);
            if (camera == null) return NotFound();

            if (await _recording.IsRecordingAsync(id))
                await _recording.StopRecordingAsync(id);

            _db.Cameras.Remove(camera);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ===== ONVIF DISCOVERY =====
        [HttpGet("discover"), Authorize(Roles = "Admin,Operator")]
        public async Task<ActionResult<List<OnvifDiscoverResponse>>> Discover()
        {
            var discovered = await _onvif.DiscoverDevicesAsync(5000);
            var existingIps = await _db.Cameras.Select(c => c.IpAddress).ToListAsync();

            var results = discovered.Select(d => new OnvifDiscoverResponse
            {
                XAddr = d.Xaddr,
                IpAddress = d.IpAddress,
                Port = d.Port,
                IsAlreadyAdded = existingIps.Contains(d.IpAddress)
            }).ToList();

            return Ok(results);
        }

        // ===== RECORDING CONTROL =====
        [HttpPost("{id}/recording/start"), Authorize(Roles = "Admin,Operator")]
        public async Task<IActionResult> StartRecording(Guid id)
        {
            try { await _recording.StartRecordingAsync(id); return Ok(new { message = "Recording started" }); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpPost("{id}/recording/stop"), Authorize(Roles = "Admin,Operator")]
        public async Task<IActionResult> StopRecording(Guid id)
        {
            await _recording.StopRecordingAsync(id);
            return Ok(new { message = "Recording stopped" });
        }

        // ===== SNAPSHOT =====
        [HttpGet("{id}/snapshot")]
        public async Task<IActionResult> GetSnapshot(Guid id)
        {
            var camera = await _db.Cameras.FindAsync(id);
            if (camera == null) return NotFound();
            try
            {
                var bytes = await _onvif.GetSnapshotAsync(camera);
                return File(bytes, "image/jpeg");
            }
            catch { return StatusCode(503, new { message = "Snapshot unavailable" }); }
        }

        private CameraDto MapToDto(Camera c) => new()
        {
            Id = c.Id, Name = c.Name, IpAddress = c.IpAddress, Port = c.Port,
            RtspUrl = c.RtspUrl, OnvifServiceUrl = c.OnvifServiceUrl,
            Manufacturer = c.Manufacturer, Model = c.Model, Status = c.Status,
            IsOnline = c.IsOnline, IsRecording = c.IsRecording, PtzCapable = c.PtzCapable,
            AudioEnabled = c.AudioEnabled, Resolution_Width = c.Resolution_Width,
            Resolution_Height = c.Resolution_Height, Framerate = c.Framerate, Codec = c.Codec,
            GridPosition = c.GridPosition, StorageProfileId = c.StorageProfileId,
            CreatedAt = c.CreatedAt, LastSeenAt = c.LastSeenAt,
            PtzPresets = c.PtzPresets?.Select(p => new PtzPresetDto
            {
                Id = p.Id, Name = p.Name, OnvifToken = p.OnvifToken,
                PanPosition = p.PanPosition, TiltPosition = p.TiltPosition, ZoomPosition = p.ZoomPosition
            }).ToList() ?? new()
        };
    }

    // ============================================================
    // PTZ CONTROLLER
    // ============================================================
    [ApiController, Route("api/cameras/{cameraId}/ptz"), Authorize]
    public class PtzController : ControllerBase
    {
        private readonly NvrDbContext _db;
        private readonly IOnvifService _onvif;

        public PtzController(NvrDbContext db, IOnvifService onvif) { _db = db; _onvif = onvif; }

        [HttpPost("move"), Authorize(Roles = "Admin,Operator")]
        public async Task<IActionResult> Move(Guid cameraId, [FromBody] PtzMoveRequest request)
        {
            var camera = await _db.Cameras.FindAsync(cameraId);
            if (camera == null || !camera.PtzCapable) return NotFound();

            switch (request.MoveType)
            {
                case "Absolute": await _onvif.PtzAbsoluteMoveAsync(camera, request.Pan, request.Tilt, request.Zoom); break;
                case "Relative": await _onvif.PtzRelativeMoveAsync(camera, request.Pan, request.Tilt, request.Zoom); break;
                default: await _onvif.PtzContinuousMoveAsync(camera, request.Pan, request.Tilt, request.Zoom); break;
            }
            return Ok();
        }

        [HttpPost("stop"), Authorize(Roles = "Admin,Operator")]
        public async Task<IActionResult> Stop(Guid cameraId)
        {
            var camera = await _db.Cameras.FindAsync(cameraId);
            if (camera == null) return NotFound();
            await _onvif.PtzStopAsync(camera);
            return Ok();
        }

        [HttpGet("presets")]
        public async Task<ActionResult<List<PtzPresetDto>>> GetPresets(Guid cameraId)
        {
            var presets = await _db.PtzPresets.Where(p => p.CameraId == cameraId).OrderBy(p => p.OrderIndex).ToListAsync();
            return Ok(presets.Select(p => new PtzPresetDto { Id = p.Id, Name = p.Name, OnvifToken = p.OnvifToken, PanPosition = p.PanPosition, TiltPosition = p.TiltPosition, ZoomPosition = p.ZoomPosition }));
        }

        [HttpPost("presets/{presetId}/goto"), Authorize(Roles = "Admin,Operator")]
        public async Task<IActionResult> GoToPreset(Guid cameraId, Guid presetId)
        {
            var camera = await _db.Cameras.FindAsync(cameraId);
            var preset = await _db.PtzPresets.FindAsync(presetId);
            if (camera == null || preset == null) return NotFound();
            await _onvif.GoToPresetAsync(camera, preset.OnvifToken);
            return Ok();
        }

        [HttpPost("presets"), Authorize(Roles = "Admin,Operator")]
        public async Task<ActionResult<PtzPresetDto>> SavePreset(Guid cameraId, [FromBody] string presetName)
        {
            var camera = await _db.Cameras.FindAsync(cameraId);
            if (camera == null) return NotFound();

            var token = await _onvif.SetPresetAsync(camera, presetName);
            var status = await _onvif.GetPtzStatusAsync(camera);

            var preset = new PtzPreset
            {
                CameraId = cameraId,
                Name = presetName,
                OnvifToken = token,
                PanPosition = status.Pan,
                TiltPosition = status.Tilt,
                ZoomPosition = status.Zoom,
                OrderIndex = await _db.PtzPresets.CountAsync(p => p.CameraId == cameraId)
            };

            _db.PtzPresets.Add(preset);
            await _db.SaveChangesAsync();
            return Ok(new PtzPresetDto { Id = preset.Id, Name = preset.Name, OnvifToken = preset.OnvifToken });
        }

        [HttpDelete("presets/{presetId}"), Authorize(Roles = "Admin,Operator")]
        public async Task<IActionResult> DeletePreset(Guid cameraId, Guid presetId)
        {
            var camera = await _db.Cameras.FindAsync(cameraId);
            var preset = await _db.PtzPresets.FindAsync(presetId);
            if (camera == null || preset == null) return NotFound();

            await _onvif.RemovePresetAsync(camera, preset.OnvifToken);
            _db.PtzPresets.Remove(preset);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }

    // ============================================================
    // RECORDINGS CONTROLLER
    // ============================================================
    [ApiController, Route("api/recordings"), Authorize]
    public class RecordingsController : ControllerBase
    {
        private readonly NvrDbContext _db;
        private readonly IStorageProviderFactory _storageFactory;

        public RecordingsController(NvrDbContext db, IStorageProviderFactory storageFactory)
        {
            _db = db; _storageFactory = storageFactory;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<RecordingDto>>> Search([FromQuery] RecordingSearchDto request)
        {
            var query = _db.Recordings.Include(r => r.Camera).AsQueryable();

            if (request.CameraId.HasValue)
                query = query.Where(r => r.CameraId == request.CameraId);
            if (request.CameraIds?.Any() == true)
                query = query.Where(r => request.CameraIds.Contains(r.CameraId));
            if (request.StartTime.HasValue)
                query = query.Where(r => r.StartTime >= request.StartTime);
            if (request.EndTime.HasValue)
                query = query.Where(r => r.StartTime <= request.EndTime);
            if (!string.IsNullOrEmpty(request.TriggerType))
                query = query.Where(r => r.TriggerType == request.TriggerType);

            query = query.Where(r => !r.IsDeleted);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.StartTime)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return Ok(new PagedResult<RecordingDto>
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = total,
                Page = request.Page,
                PageSize = request.PageSize
            });
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RecordingDto>> Get(Guid id)
        {
            var recording = await _db.Recordings.Include(r => r.Camera).FirstOrDefaultAsync(r => r.Id == id);
            if (recording == null || recording.IsDeleted) return NotFound();
            return Ok(MapToDto(recording));
        }

        // ===== PLAYBACK: Get chunks for a time range (multi-camera sync) =====
        [HttpPost("playback")]
        public async Task<ActionResult<PlaybackSessionDto>> GetPlayback([FromBody] PlaybackRequest request)
        {
            var sessionId = Guid.NewGuid().ToString();
            var cameraStreams = new List<CameraPlaybackInfo>();

            foreach (var cameraId in request.CameraIds)
            {
                var camera = await _db.Cameras.FindAsync(cameraId);
                if (camera == null) continue;

                // Find recording at requested timestamp
                var recording = await _db.Recordings
                    .Where(r => r.CameraId == cameraId &&
                                r.StartTime <= request.Timestamp &&
                                (r.EndTime == null || r.EndTime >= request.Timestamp) &&
                                !r.IsDeleted)
                    .FirstOrDefaultAsync();

                // Build timeline for camera (segments with/without recordings)
                var timeline = await GetCameraTimelineAsync(cameraId, request.Timestamp.AddHours(-12), request.Timestamp.AddHours(12));

                cameraStreams.Add(new CameraPlaybackInfo
                {
                    CameraId = cameraId,
                    CameraName = camera.Name,
                    RecordingId = recording?.Id,
                    HasRecording = recording != null,
                    StreamUrl = recording != null ? $"/api/recordings/{recording.Id}/stream?t={request.Timestamp:O}" : null,
                    Timeline = timeline
                });
            }

            return Ok(new PlaybackSessionDto
            {
                SessionId = sessionId,
                CameraStreams = cameraStreams,
                StartTimestamp = request.Timestamp
            });
        }

        // ===== STREAM: Serve recording chunks =====
        [HttpGet("{id}/stream")]
        public async Task<IActionResult> StreamRecording(Guid id, [FromQuery] DateTime? t = null)
        {
            var recording = await _db.Recordings.Include(r => r.StorageProfile).FirstOrDefaultAsync(r => r.Id == id);
            if (recording == null || recording.IsDeleted) return NotFound();

            // Find the chunk matching the requested timestamp
            var query = _db.RecordingChunks.Where(c => c.RecordingId == id);
            if (t.HasValue)
                query = query.Where(c => c.StartTime <= t && c.EndTime >= t);

            var chunk = await query.OrderBy(c => c.SequenceNumber).FirstOrDefaultAsync();
            if (chunk == null) return NotFound("No recording chunk found for requested time");

            var provider = _storageFactory.GetProvider(recording.StorageProfile!);
            var stream = await provider.OpenReadAsync(recording.StorageProfile!, chunk.FilePath);

            // Stream MPEG-TS content
            return File(stream, "video/mp2t", enableRangeProcessing: true);
        }

        // ===== DELETE RECORDING =====
        [HttpDelete("{id}"), Authorize(Roles = "Admin,Operator")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var recording = await _db.Recordings.Include(r => r.StorageProfile).FirstOrDefaultAsync(r => r.Id == id);
            if (recording == null) return NotFound();

            var chunks = await _db.RecordingChunks.Where(c => c.RecordingId == id).ToListAsync();
            var provider = _storageFactory.GetProvider(recording.StorageProfile!);

            foreach (var chunk in chunks)
                await provider.DeleteAsync(recording.StorageProfile!, chunk.FilePath);

            _db.RecordingChunks.RemoveRange(chunks);
            recording.IsDeleted = true;
            recording.Status = "Deleted";
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private async Task<List<TimelineSegmentDto>> GetCameraTimelineAsync(Guid cameraId, DateTime from, DateTime to)
        {
            var recordings = await _db.Recordings
                .Where(r => r.CameraId == cameraId && r.StartTime < to && (r.EndTime ?? DateTime.UtcNow) > from && !r.IsDeleted)
                .OrderBy(r => r.StartTime)
                .ToListAsync();

            return recordings.Select(r => new TimelineSegmentDto
            {
                Start = r.StartTime,
                End = r.EndTime ?? DateTime.UtcNow,
                TriggerType = r.TriggerType
            }).ToList();
        }

        private RecordingDto MapToDto(Recording r) => new()
        {
            Id = r.Id, CameraId = r.CameraId, CameraName = r.Camera?.Name ?? string.Empty,
            StartTime = r.StartTime, EndTime = r.EndTime, FileSizeBytes = r.FileSizeBytes,
            DurationSeconds = r.DurationSeconds, Status = r.Status, TriggerType = r.TriggerType,
            ThumbnailPath = r.ThumbnailPath, Width = r.Width, Height = r.Height, ChunkCount = r.ChunkCount
        };
    }

    // ============================================================
    // STORAGE CONTROLLER
    // ============================================================
    [ApiController, Route("api/storage"), Authorize(Roles = "Admin")]
    public class StorageController : ControllerBase
    {
        private readonly NvrDbContext _db;
        private readonly IStorageProviderFactory _storageFactory;

        public StorageController(NvrDbContext db, IStorageProviderFactory storageFactory)
        {
            _db = db; _storageFactory = storageFactory;
        }

        [HttpGet]
        public async Task<ActionResult<List<StorageProfileDto>>> GetAll()
        {
            var profiles = await _db.StorageProfiles.ToListAsync();
            return Ok(profiles.Select(MapToDto));
        }

        [HttpGet("types")]
        public ActionResult<IEnumerable<string>> GetTypes() =>
            Ok(_storageFactory.GetAvailableProviderTypes());

        [HttpPost]
        public async Task<ActionResult<StorageProfileDto>> Create([FromBody] CreateStorageProfileRequest request)
        {
            var profile = new StorageProfile
            {
                Name = request.Name, Type = request.Type, IsDefault = request.IsDefault,
                Host = request.Host, Port = request.Port, Username = request.Username,
                Password = request.Password, BasePath = request.BasePath,
                ShareName = request.ShareName, Region = request.Region,
                AccessKey = request.AccessKey, SecretKey = request.SecretKey,
                ContainerName = request.ContainerName, ConnectionString = request.ConnectionString,
                MaxStorageBytes = request.MaxStorageBytes, RetentionDays = request.RetentionDays,
                AutoDeleteEnabled = request.AutoDeleteEnabled
            };

            if (request.IsDefault)
            {
                var existing = await _db.StorageProfiles.Where(s => s.IsDefault).ToListAsync();
                existing.ForEach(s => s.IsDefault = false);
            }

            _db.StorageProfiles.Add(profile);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAll), MapToDto(profile));
        }

        [HttpPost("{id}/test")]
        public async Task<IActionResult> TestConnection(Guid id)
        {
            var profile = await _db.StorageProfiles.FindAsync(id);
            if (profile == null) return NotFound();

            var provider = _storageFactory.GetProvider(profile);
            var success = await provider.TestConnectionAsync(profile);
            return Ok(new { Success = success, Message = success ? "Connection successful" : "Connection failed" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var profile = await _db.StorageProfiles.Include(s => s.Cameras).FirstOrDefaultAsync(s => s.Id == id);
            if (profile == null) return NotFound();
            if (profile.Cameras.Any()) return Conflict(new { message = "Storage profile has cameras assigned. Reassign them first." });

            _db.StorageProfiles.Remove(profile);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private StorageProfileDto MapToDto(StorageProfile s) => new()
        {
            Id = s.Id, Name = s.Name, Type = s.Type, IsDefault = s.IsDefault, IsEnabled = s.IsEnabled,
            Host = s.Host, Port = s.Port, Username = s.Username, BasePath = s.BasePath,
            ShareName = s.ShareName, Region = s.Region, ContainerName = s.ContainerName,
            MaxStorageBytes = s.MaxStorageBytes, UsedStorageBytes = s.UsedStorageBytes,
            RetentionDays = s.RetentionDays, AutoDeleteEnabled = s.AutoDeleteEnabled,
            IsHealthy = s.IsHealthy, LastHealthCheck = s.LastHealthCheck, HealthError = s.HealthError
        };
    }

    // ============================================================
    // LAYOUT CONTROLLER
    // ============================================================
    [ApiController, Route("api/layout"), Authorize]
    public class LayoutController : ControllerBase
    {
        private readonly NvrDbContext _db;

        public LayoutController(NvrDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<LayoutSaveRequest>> GetLayout([FromQuery] string layoutName = "Default")
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var layouts = await _db.UserCameraLayouts
                .Where(l => l.UserId == userId && l.LayoutName == layoutName)
                .OrderBy(l => l.GridPosition)
                .ToListAsync();

            if (!layouts.Any()) return Ok(new LayoutSaveRequest { LayoutName = layoutName, GridColumns = 4, Positions = new() });

            return Ok(new LayoutSaveRequest
            {
                LayoutName = layoutName,
                GridColumns = layouts.First().GridColumns,
                Positions = layouts.Select(l => new CameraPositionRequest { CameraId = l.CameraId, GridPosition = l.GridPosition }).ToList()
            });
        }

        [HttpPost]
        public async Task<IActionResult> SaveLayout([FromBody] LayoutSaveRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            // Remove existing layout
            var existing = _db.UserCameraLayouts.Where(l => l.UserId == userId && l.LayoutName == request.LayoutName);
            _db.UserCameraLayouts.RemoveRange(existing);

            // Add new layout
            var layouts = request.Positions.Select(p => new UserCameraLayout
            {
                UserId = userId,
                CameraId = p.CameraId,
                GridPosition = p.GridPosition,
                GridColumns = request.GridColumns,
                LayoutName = request.LayoutName
            });

            _db.UserCameraLayouts.AddRange(layouts);
            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("names")]
        public async Task<ActionResult<List<string>>> GetLayoutNames()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var names = await _db.UserCameraLayouts.Where(l => l.UserId == userId).Select(l => l.LayoutName).Distinct().ToListAsync();
            return Ok(names);
        }
    }

    // ============================================================
    // SCHEDULES CONTROLLER
    // ============================================================
    [ApiController, Route("api/cameras/{cameraId}/schedules"), Authorize]
    public class SchedulesController : ControllerBase
    {
        private readonly NvrDbContext _db;

        public SchedulesController(NvrDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<List<RecordingScheduleDto>>> GetAll(Guid cameraId)
        {
            var schedules = await _db.RecordingSchedules.Where(s => s.CameraId == cameraId).ToListAsync();
            return Ok(schedules.Select(s => new RecordingScheduleDto
            {
                Id = s.Id, CameraId = s.CameraId, Name = s.Name, IsEnabled = s.IsEnabled,
                DaysOfWeek = s.DaysOfWeek, StartTime = s.StartTime, EndTime = s.EndTime,
                RecordingMode = s.RecordingMode, ChunkDurationSeconds = s.ChunkDurationSeconds,
                BitrateKbps = s.BitrateKbps, Quality = s.Quality
            }));
        }

        [HttpPost, Authorize(Roles = "Admin,Operator")]
        public async Task<IActionResult> Create(Guid cameraId, [FromBody] RecordingScheduleDto request)
        {
            var schedule = new RecordingSchedule
            {
                CameraId = cameraId, Name = request.Name, IsEnabled = request.IsEnabled,
                DaysOfWeek = request.DaysOfWeek, StartTime = request.StartTime, EndTime = request.EndTime,
                RecordingMode = request.RecordingMode, ChunkDurationSeconds = request.ChunkDurationSeconds,
                BitrateKbps = request.BitrateKbps, Quality = request.Quality
            };
            _db.RecordingSchedules.Add(schedule);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAll), new { cameraId }, request);
        }

        [HttpDelete("{id}"), Authorize(Roles = "Admin,Operator")]
        public async Task<IActionResult> Delete(Guid cameraId, Guid id)
        {
            var schedule = await _db.RecordingSchedules.FirstOrDefaultAsync(s => s.Id == id && s.CameraId == cameraId);
            if (schedule == null) return NotFound();
            _db.RecordingSchedules.Remove(schedule);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }

    // ============================================================
    // DASHBOARD CONTROLLER
    // ============================================================
    [ApiController, Route("api/dashboard"), Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly NvrDbContext _db;

        public DashboardController(NvrDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<DashboardSummaryDto>> GetSummary()
        {
            var today = DateTime.UtcNow.Date;
            var cameras = await _db.Cameras.ToListAsync();
            var storage = await _db.StorageProfiles.Where(s => s.IsEnabled).ToListAsync();

            return Ok(new DashboardSummaryDto
            {
                TotalCameras = cameras.Count,
                OnlineCameras = cameras.Count(c => c.IsOnline),
                RecordingCameras = cameras.Count(c => c.IsRecording),
                OfflineCameras = cameras.Count(c => !c.IsOnline),
                TotalStorageBytes = storage.Sum(s => s.MaxStorageBytes),
                UsedStorageBytes = storage.Sum(s => s.UsedStorageBytes),
                ActiveAlerts = await _db.CameraEvents.CountAsync(e => !e.IsAcknowledged && e.Timestamp > DateTime.UtcNow.AddHours(-24)),
                TodayRecordingCount = await _db.Recordings.CountAsync(r => r.StartTime >= today),
                StorageSummaries = storage.Select(s => new StorageProfileDto
                {
                    Id = s.Id, Name = s.Name, Type = s.Type, MaxStorageBytes = s.MaxStorageBytes,
                    UsedStorageBytes = s.UsedStorageBytes, IsHealthy = s.IsHealthy
                }).ToList()
            });
        }

        [HttpGet("events")]
        public async Task<ActionResult<List<object>>> GetRecentEvents([FromQuery] int count = 50)
        {
            var events = await _db.CameraEvents
                .Include(e => e.Camera)
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .Select(e => new
                {
                    e.Id, e.CameraId, CameraName = e.Camera!.Name, e.EventType,
                    e.Severity, e.Timestamp, e.Details, e.IsAcknowledged
                })
                .ToListAsync();

            return Ok(events);
        }
    }

    // ============================================================
    // SETTINGS CONTROLLER
    // ============================================================
    [ApiController, Route("api/settings"), Authorize(Roles = "Admin")]
    public class SettingsController : ControllerBase
    {
        private readonly NvrDbContext _db;

        public SettingsController(NvrDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<Dictionary<string, string>>> GetAll()
        {
            var settings = await _db.SystemSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
            return Ok(settings);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] Dictionary<string, string> settings)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            foreach (var (key, value) in settings)
            {
                var existing = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
                if (existing != null) { existing.Value = value; existing.UpdatedAt = DateTime.UtcNow; existing.UpdatedBy = userId; }
                else _db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, UpdatedBy = userId });
            }
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}
