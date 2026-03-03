using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NVR.Core.Interfaces;

namespace NVR.Infrastructure.Services
{
    /// <summary>
    /// RTSP stream service using FFmpeg as subprocess
    /// Outputs raw H.264 frames for SignalR delivery
    /// Standard YouTube-like approach: MPEG-DASH or raw frames via WebSockets
    /// </summary>
    public class RtspStreamService : IRtspStreamService, IDisposable
    {
        private readonly ILogger<RtspStreamService> _logger;
        private readonly ConcurrentDictionary<Guid, StreamSession> _sessions = new();
        private bool _disposed;

        public RtspStreamService(ILogger<RtspStreamService> logger) => _logger = logger;

        public async Task StartStreamAsync(Guid cameraId, CancellationToken ct = default)
        {
            if (_sessions.ContainsKey(cameraId)) return;

            var session = new StreamSession(cameraId);
            if (!_sessions.TryAdd(cameraId, session))
            {
                session.Dispose();
                return;
            }

            _logger.LogInformation("Starting RTSP stream for camera {CameraId}", cameraId);
            _ = Task.Run(() => session.RunAsync(ct), ct);
        }

        public Task StopStreamAsync(Guid cameraId)
        {
            if (_sessions.TryRemove(cameraId, out var session))
            {
                session.Dispose();
                _logger.LogInformation("Stopped RTSP stream for camera {CameraId}", cameraId);
            }
            return Task.CompletedTask;
        }

        public Task<bool> IsStreamingAsync(Guid cameraId) =>
            Task.FromResult(_sessions.ContainsKey(cameraId));

        public async IAsyncEnumerable<byte[]> GetFrameStreamAsync(Guid cameraId,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            if (!_sessions.TryGetValue(cameraId, out var session))
                yield break;

            await foreach (var frame in session.FrameChannel.Reader.ReadAllAsync(ct))
                yield return frame;
        }

        public Task<byte[]> GetLatestFrameAsync(Guid cameraId, CancellationToken ct = default)
        {
            if (_sessions.TryGetValue(cameraId, out var session) && session.LatestFrame != null)
                return Task.FromResult(session.LatestFrame);
            return Task.FromResult(Array.Empty<byte>());
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var session in _sessions.Values) session.Dispose();
            _sessions.Clear();
        }

        // ===== INNER CLASS =====
        private class StreamSession : IDisposable
        {
            public Guid CameraId { get; }
            public string? RtspUrl { get; set; }
            public Channel<byte[]> FrameChannel { get; } = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(30) { FullMode = BoundedChannelFullMode.DropOldest });
            public byte[]? LatestFrame { get; private set; }

            private Process? _ffmpegProcess;
            private CancellationTokenSource _cts = new();
            private bool _disposed;

            public StreamSession(Guid cameraId) => CameraId = cameraId;

            public async Task RunAsync(CancellationToken externalCt)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(externalCt, _cts.Token);
                var ct = linked.Token;

                while (!ct.IsCancellationRequested)
                {
                    try { await StreamLoopAsync(ct); }
                    catch (OperationCanceledException) { break; }
                    catch (Exception) { await Task.Delay(5000, ct); } // Reconnect delay
                }
            }

            private async Task StreamLoopAsync(CancellationToken ct)
            {
                if (string.IsNullOrEmpty(RtspUrl)) { await Task.Delay(1000, ct); return; }

                // FFmpeg command: RTSP → MJPEG frames piped to stdout
                // This is the standard approach for web browser streaming without HLS
                // Each frame = complete JPEG image for low latency delivery
                var args = string.Join(" ", new[]
                {
                    "-loglevel error",
                    "-rtsp_transport tcp",       // TCP for reliable delivery
                    $"-i \"{RtspUrl}\"",
                    "-vf scale=1280:720",         // Normalize resolution
                    "-q:v 5",                     // JPEG quality (1=best, 31=worst)
                    "-r 15",                      // 15fps for streaming (balance quality/bandwidth)
                    "-f image2pipe",              // Output as image sequence
                    "-vcodec mjpeg",              // MJPEG format
                    "pipe:1"                      // Output to stdout
                });

                _ffmpegProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                _ffmpegProcess.Start();

                using var stdout = _ffmpegProcess.StandardOutput.BaseStream;
                var buffer = new byte[1024 * 1024]; // 1MB buffer
                var frameBuffer = new MemoryStream();
                int prevByte = -1;

                while (!ct.IsCancellationRequested)
                {
                    int b = stdout.ReadByte();
                    if (b == -1) break;

                    frameBuffer.WriteByte((byte)b);

                    // JPEG SOI marker = 0xFF 0xD8, EOI = 0xFF 0xD9
                    // Detect complete JPEG frame
                    if (prevByte == 0xFF && b == 0xD9 && frameBuffer.Length > 2)
                    {
                        var frame = frameBuffer.ToArray();
                        LatestFrame = frame;
                        FrameChannel.Writer.TryWrite(frame);
                        frameBuffer = new MemoryStream();
                    }
                    prevByte = b;
                }

                if (!_ffmpegProcess.HasExited)
                    _ffmpegProcess.Kill();
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _cts.Cancel();
                _cts.Dispose();
                FrameChannel.Writer.TryComplete();
                if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
                {
                    _ffmpegProcess.Kill();
                    _ffmpegProcess.Dispose();
                }
            }
        }
    }
}
