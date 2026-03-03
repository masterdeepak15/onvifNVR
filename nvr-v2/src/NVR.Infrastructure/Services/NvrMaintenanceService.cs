using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NVR.Core.Interfaces;
using NVR.Infrastructure.Data;

namespace NVR.Infrastructure.Services
{
    /// <summary>
    /// NVR Maintenance Background Service
    /// Runs every 5 minutes:
    /// ─ Auto-delete expired recordings (retention policy)
    /// ─ Storage quota enforcement (delete oldest when full)
    /// ─ Camera health monitoring (TCP ping)
    /// ─ Storage provider health checks
    /// ─ Schedule-based recording start/stop
    /// ─ SignalR real-time push to dashboard (CameraStatus, StorageAlert, AnalyticsUpdate)
    ///
    /// Runs hourly:
    /// ─ Analytics snapshot (uptime, recording stats, viewer counts)
    /// ─ Clean up expired stream sessions
    /// ─ Expire camera access grants
    /// </summary>
    public class NvrMaintenanceService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<NvrMaintenanceService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
        private DateTime _lastHourlyRun = DateTime.MinValue;

        public NvrMaintenanceService(IServiceProvider sp, ILogger<NvrMaintenanceService> logger)
        {
            _sp = sp;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            _logger.LogInformation("NVR Maintenance Service started");

            // Wait for app to fully start before first run
            await Task.Delay(TimeSpan.FromSeconds(10), ct);

            while (!ct.IsCancellationRequested)
            {
                try { await RunMaintenanceAsync(ct); }
                catch (Exception ex) { _logger.LogError(ex, "Maintenance cycle error"); }

                await Task.Delay(_interval, ct);
            }
        }

        private async Task RunMaintenanceAsync(CancellationToken ct)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NvrDbContext>();
            var storageFactory = scope.ServiceProvider.GetRequiredService<IStorageProviderFactory>();
            var emitter = scope.ServiceProvider.GetRequiredService<INvrHubEventEmitter>();
            var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();

            await CheckCameraHealthAsync(db, emitter, ct);
            await CheckStorageHealthAsync(db, storageFactory, emitter, ct);
            await AutoDeleteExpiredRecordingsAsync(db, storageFactory, ct);
            await EnforceStorageQuotasAsync(db, storageFactory, ct);
            await RunScheduledRecordingsAsync(db, scope.ServiceProvider, ct);
            await ExpireAccessGrantsAsync(db, ct);
            await CleanupStreamSessionsAsync(db, ct);

            // Hourly tasks
            if ((DateTime.UtcNow - _lastHourlyRun).TotalHours >= 1)
            {
                await analytics.TakeHourlySnapshotAsync(ct);
                await PushAnalyticsUpdateAsync(db, emitter, ct);
                _lastHourlyRun = DateTime.UtcNow;
            }
        }

        // ============================================================
        // CAMERA HEALTH
        // ============================================================
        private async Task CheckCameraHealthAsync(NvrDbContext db, INvrHubEventEmitter emitter, CancellationToken ct)
        {
            var cameras = await db.Cameras.ToListAsync(ct);

            foreach (var camera in cameras)
            {
                bool wasOnline = camera.IsOnline;
                bool isNowOnline = false;

                try
                {
                    using var tcp = new System.Net.Sockets.TcpClient();
                    var connectTask = tcp.ConnectAsync(camera.IpAddress, camera.Port, ct).AsTask();
                    if (await Task.WhenAny(connectTask, Task.Delay(3000, ct)) == connectTask && !connectTask.IsFaulted)
                    {
                        isNowOnline = true;
                        camera.IsOnline = true;
                        camera.Status = "Online";
                        camera.LastSeenAt = DateTime.UtcNow;
                        camera.LastError = null;
                    }
                    else
                    {
                        camera.IsOnline = false;
                        camera.Status = "Offline";
                    }
                }
                catch (Exception ex)
                {
                    camera.IsOnline = false;
                    camera.Status = "Error";
                    camera.LastError = ex.Message;
                }

                // Log state transitions as events
                if (wasOnline && !camera.IsOnline)
                {
                    db.CameraEvents.Add(new Core.Entities.CameraEvent
                    {
                        CameraId = camera.Id,
                        EventType = "Offline",
                        Severity = "Warning",
                        Details = "Camera went offline"
                    });
                    await emitter.SendAlertAsync(camera.Id, camera.Name, "CameraOffline", $"{camera.Name} is offline", "Warning");
                }
                else if (!wasOnline && camera.IsOnline)
                {
                    db.CameraEvents.Add(new Core.Entities.CameraEvent
                    {
                        CameraId = camera.Id,
                        EventType = "Online",
                        Severity = "Info",
                        Details = "Camera back online"
                    });
                }

                // Notify all watchers of status
                await emitter.SendCameraStatusAsync(camera.Id, camera.Status, camera.IsOnline, camera.IsRecording);
            }

            await db.SaveChangesAsync(ct);
        }

        // ============================================================
        // STORAGE HEALTH
        // ============================================================
        private async Task CheckStorageHealthAsync(NvrDbContext db, IStorageProviderFactory factory, INvrHubEventEmitter emitter, CancellationToken ct)
        {
            var profiles = await db.StorageProfiles.Where(s => s.IsEnabled).ToListAsync(ct);

            foreach (var profile in profiles)
            {
                try
                {
                    var provider = factory.GetProvider(profile);
                    profile.IsHealthy = await provider.TestConnectionAsync(profile, ct);
                    profile.UsedStorageBytes = await provider.GetUsedSpaceAsync(profile, ct);
                    profile.LastHealthCheck = DateTime.UtcNow;
                    profile.HealthError = profile.IsHealthy ? null : "Connection test failed";

                    // Warn if storage is getting full
                    if (profile.MaxStorageBytes > 0)
                    {
                        var usagePct = (double)profile.UsedStorageBytes / profile.MaxStorageBytes * 100;
                        if (usagePct >= profile.LowSpaceWarningPercent)
                        {
                            await emitter.SendStorageAlertAsync(profile.Id, usagePct,
                                $"Storage '{profile.Name}' is {usagePct:F1}% full");
                        }
                    }
                }
                catch (Exception ex)
                {
                    profile.IsHealthy = false;
                    profile.HealthError = ex.Message;
                    profile.LastHealthCheck = DateTime.UtcNow;
                    await emitter.SendAlertAsync(null, string.Empty, "StorageError", $"Storage '{profile.Name}' error: {ex.Message}", "Critical");
                }
            }

            await db.SaveChangesAsync(ct);
        }

        // ============================================================
        // AUTO-DELETE (retention policy)
        // ============================================================
        private async Task AutoDeleteExpiredRecordingsAsync(NvrDbContext db, IStorageProviderFactory factory, CancellationToken ct)
        {
            var profiles = await db.StorageProfiles.Where(s => s.AutoDeleteEnabled && s.IsEnabled).ToListAsync(ct);

            foreach (var profile in profiles)
            {
                var cutoff = DateTime.UtcNow.AddDays(-profile.RetentionDays);
                var expired = await db.Recordings
                    .Where(r => r.StorageProfileId == profile.Id && r.StartTime < cutoff && !r.IsDeleted && r.Status == "Completed")
                    .ToListAsync(ct);

                foreach (var rec in expired)
                {
                    try
                    {
                        var chunks = await db.RecordingChunks.Where(c => c.RecordingId == rec.Id).ToListAsync(ct);
                        var provider = factory.GetProvider(profile);
                        foreach (var chunk in chunks)
                            try { await provider.DeleteAsync(profile, chunk.FilePath, ct); } catch { }
                        db.RecordingChunks.RemoveRange(chunks);
                        rec.IsDeleted = true;
                        rec.Status = "Deleted";
                        _logger.LogInformation("Auto-deleted recording {Id} (expired after {Days} days)", rec.Id, profile.RetentionDays);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete recording {Id}", rec.Id);
                    }
                }
                await db.SaveChangesAsync(ct);
            }
        }

        // ============================================================
        // QUOTA ENFORCEMENT
        // ============================================================
        private async Task EnforceStorageQuotasAsync(NvrDbContext db, IStorageProviderFactory factory, CancellationToken ct)
        {
            var profiles = await db.StorageProfiles.Where(s => s.IsEnabled && s.AutoDeleteEnabled).ToListAsync(ct);

            foreach (var profile in profiles)
            {
                if (profile.MaxStorageBytes <= 0) continue;
                double usagePct = (double)profile.UsedStorageBytes / profile.MaxStorageBytes * 100;
                if (usagePct < 95) continue;

                _logger.LogWarning("Storage {Name} at {Pct:F1}% — enforcing quota", profile.Name, usagePct);
                long target = (long)(profile.MaxStorageBytes * 0.80);
                long used = profile.UsedStorageBytes;

                var oldest = await db.Recordings
                    .Where(r => r.StorageProfileId == profile.Id && !r.IsDeleted && r.Status == "Completed")
                    .OrderBy(r => r.StartTime).Take(50).ToListAsync(ct);

                foreach (var rec in oldest)
                {
                    if (used <= target) break;
                    var chunks = await db.RecordingChunks.Where(c => c.RecordingId == rec.Id).ToListAsync(ct);
                    var provider = factory.GetProvider(profile);
                    foreach (var chunk in chunks)
                        try { await provider.DeleteAsync(profile, chunk.FilePath, ct); } catch { }
                    db.RecordingChunks.RemoveRange(chunks);
                    used -= rec.FileSizeBytes;
                    rec.IsDeleted = true; rec.Status = "Deleted";
                }
                await db.SaveChangesAsync(ct);
            }
        }

        // ============================================================
        // SCHEDULED RECORDINGS
        // ============================================================
        private async Task RunScheduledRecordingsAsync(NvrDbContext db, IServiceProvider services, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var nowTime = now.TimeOfDay;
            int dayBit = (int)Math.Pow(2, (int)now.DayOfWeek);

            var schedules = await db.RecordingSchedules
                .Include(s => s.Camera)
                .Where(s => s.IsEnabled && (s.DaysOfWeek & dayBit) != 0)
                .ToListAsync(ct);

            var recorder = services.GetRequiredService<IRecordingService>();

            foreach (var sched in schedules)
            {
                if (sched.Camera == null) continue;
                bool shouldRecord = nowTime >= sched.StartTime && nowTime < sched.EndTime;
                bool isRecording = await recorder.IsRecordingAsync(sched.CameraId);

                if (shouldRecord && !isRecording)
                    await recorder.StartRecordingAsync(sched.CameraId, ct);
                else if (!shouldRecord && isRecording)
                    await recorder.StopRecordingAsync(sched.CameraId);
            }
        }

        // ============================================================
        // EXPIRE ACCESS GRANTS
        // ============================================================
        private async Task ExpireAccessGrantsAsync(NvrDbContext db, CancellationToken ct)
        {
            var expired = await db.CameraUserAccesses
                .Where(a => a.IsActive && a.ExpiresAt != null && a.ExpiresAt < DateTime.UtcNow)
                .ToListAsync(ct);

            foreach (var grant in expired)
                grant.IsActive = false;

            if (expired.Any())
            {
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Expired {Count} camera access grants", expired.Count);
            }
        }

        // ============================================================
        // CLEANUP STALE STREAM SESSIONS
        // ============================================================
        private async Task CleanupStreamSessionsAsync(NvrDbContext db, CancellationToken ct)
        {
            // Mark sessions as ended if they've been inactive for 10 minutes
            var staleTime = DateTime.UtcNow.AddMinutes(-10);
            var stale = await db.StreamSessions
                .Where(s => s.IsActive && s.StartedAt < staleTime)
                .ToListAsync(ct);

            foreach (var s in stale) { s.IsActive = false; s.EndedAt = DateTime.UtcNow; }
            if (stale.Any()) await db.SaveChangesAsync(ct);
        }

        // ============================================================
        // ANALYTICS PUSH
        // ============================================================
        private async Task PushAnalyticsUpdateAsync(NvrDbContext db, INvrHubEventEmitter emitter, CancellationToken ct)
        {
            var cameras = await db.Cameras.ToListAsync(ct);
            var storage = await db.StorageProfiles.Where(s => s.IsEnabled).ToListAsync(ct);
            var viewers = await db.StreamSessions.CountAsync(s => s.IsActive, ct);
            var unacked = await db.CameraEvents.CountAsync(e => !e.IsAcknowledged && e.Timestamp > DateTime.UtcNow.AddHours(-24), ct);

            long total = storage.Sum(s => s.MaxStorageBytes);
            long used = storage.Sum(s => s.UsedStorageBytes);
            double pct = total > 0 ? (double)used / total * 100 : 0;

            await emitter.SendAnalyticsUpdateAsync(
                cameras.Count(c => c.IsOnline),
                cameras.Count(c => c.IsRecording),
                viewers, pct, unacked);
        }
    }
}
