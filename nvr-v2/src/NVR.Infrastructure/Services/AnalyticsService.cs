using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NVR.Core.DTOs;
using NVR.Core.Entities;
using NVR.Core.Interfaces;
using NVR.Infrastructure.Data;

namespace NVR.Infrastructure.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly NvrDbContext _db;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(NvrDbContext db, ILogger<AnalyticsService> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ============================================================
        // MAIN DASHBOARD SUMMARY
        // ============================================================
        public async Task<NvrAnalyticsSummaryDto> GetSummaryAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var todayStart = now.Date;
            var weekStart = now.Date.AddDays(-7);

            var cameras = await _db.Cameras.ToListAsync(ct);
            var storageProfiles = await _db.StorageProfiles.Where(s => s.IsEnabled).ToListAsync(ct);

            // Recording stats today
            var todayRecordings = await _db.Recordings
                .Where(r => r.StartTime >= todayStart && !r.IsDeleted)
                .ToListAsync(ct);

            var weekRecordings = await _db.Recordings
                .Where(r => r.StartTime >= weekStart && !r.IsDeleted)
                .ToListAsync(ct);

            // Active stream sessions
            var activeLiveSessions = await _db.StreamSessions
                .Where(s => s.IsActive && s.SessionType == "Live")
                .ToListAsync(ct);

            var activePlaybackSessions = await _db.StreamSessions
                .Where(s => s.IsActive && s.SessionType == "Playback")
                .ToListAsync(ct);

            // Alerts today
            var todayAlerts = await _db.CameraEvents
                .Where(e => e.Timestamp >= todayStart)
                .ToListAsync(ct);

            // Peak viewers today (from analytics snapshots)
            var peakViewers = await _db.CameraAnalyticsSnapshots
                .Where(s => s.Hour >= todayStart)
                .MaxAsync(s => (int?)s.ViewerCount, ct) ?? 0;

            // Storage
            long totalStorage = storageProfiles.Sum(s => s.MaxStorageBytes);
            long usedStorage = storageProfiles.Sum(s => s.UsedStorageBytes);

            // Bytes written today
            var bytesToday = todayRecordings.Sum(r => r.FileSizeBytes);

            // Estimated days remaining
            long dailyWriteRate = bytesToday > 0 ? bytesToday : 1;
            long freeSpace = totalStorage - usedStorage;
            int estDays = (int)(freeSpace / dailyWriteRate);

            // Per-camera breakdown
            var cameraBreakdown = new List<CameraAnalyticsSummaryDto>();
            foreach (var cam in cameras)
            {
                var camRecordings = todayRecordings.Where(r => r.CameraId == cam.Id).ToList();
                var camAlerts = todayAlerts.Where(e => e.CameraId == cam.Id).ToList();
                var camViewers = activeLiveSessions.Count(s => s.CameraId == cam.Id);

                var snapshot = await _db.CameraAnalyticsSnapshots
                    .Where(s => s.CameraId == cam.Id && s.Hour >= todayStart)
                    .OrderByDescending(s => s.Hour)
                    .FirstOrDefaultAsync(ct);

                cameraBreakdown.Add(new CameraAnalyticsSummaryDto
                {
                    CameraId = cam.Id,
                    CameraName = cam.Name,
                    Status = cam.Status,
                    IsRecording = cam.IsRecording,
                    UptimePercent = snapshot?.UptimePercent ?? (cam.IsOnline ? 100f : 0f),
                    RecordingSeconds = camRecordings.Sum(r => r.DurationSeconds),
                    StorageBytesUsed = camRecordings.Sum(r => r.FileSizeBytes),
                    MotionEvents = camAlerts.Count(e => e.EventType == "Motion"),
                    ActiveViewers = camViewers,
                    LastMotionAt = camAlerts.Where(e => e.EventType == "Motion").OrderByDescending(e => e.Timestamp).FirstOrDefault()?.Timestamp,
                    LastSeenAt = cam.LastSeenAt,
                    AvgBitrateKbps = snapshot?.AvgBitrateKbps ?? 0f
                });
            }

            return new NvrAnalyticsSummaryDto
            {
                TotalCameras = cameras.Count,
                OnlineCameras = cameras.Count(c => c.IsOnline),
                OfflineCameras = cameras.Count(c => !c.IsOnline),
                RecordingCameras = cameras.Count(c => c.IsRecording),
                SystemUptimePercent = cameras.Count > 0 ? (float)cameras.Count(c => c.IsOnline) / cameras.Count * 100 : 0,

                TotalStorageBytes = totalStorage,
                UsedStorageBytes = usedStorage,
                StorageUsagePercent = totalStorage > 0 ? (double)usedStorage / totalStorage * 100 : 0,
                StorageBytesWrittenToday = bytesToday,
                EstimatedDaysRemaining = estDays,

                TotalRecordingHoursToday = todayRecordings.Sum(r => r.DurationSeconds) / 3600,
                TotalRecordingHoursWeek = weekRecordings.Sum(r => r.DurationSeconds) / 3600,
                TotalRecordingsToday = todayRecordings.Count,
                ActiveRecordings = cameras.Count(c => c.IsRecording),

                TotalAlertsToday = todayAlerts.Count,
                UnacknowledgedAlerts = todayAlerts.Count(a => !a.IsAcknowledged),
                MotionEventsToday = todayAlerts.Count(a => a.EventType == "Motion"),
                CameraErrorsToday = todayAlerts.Count(a => a.EventType == "Error"),

                ActiveLiveViewers = activeLiveSessions.Count,
                ActivePlaybackSessions = activePlaybackSessions.Count,
                PeakViewersToday = peakViewers,

                CameraBreakdown = cameraBreakdown,
                StorageBreakdown = storageProfiles.Select(s => new StorageAnalyticsDto
                {
                    ProfileId = s.Id,
                    ProfileName = s.Name,
                    Type = s.Type,
                    TotalBytes = s.MaxStorageBytes,
                    UsedBytes = s.UsedStorageBytes,
                    UsagePercent = s.MaxStorageBytes > 0 ? (double)s.UsedStorageBytes / s.MaxStorageBytes * 100 : 0,
                    RetentionDays = s.RetentionDays,
                    EstimatedDaysRemaining = s.MaxStorageBytes > 0
                        ? (int)((s.MaxStorageBytes - s.UsedStorageBytes) / Math.Max(bytesToday, 1))
                        : 999,
                    IsHealthy = s.IsHealthy,
                    BytesWrittenToday = bytesToday
                }).ToList(),

                AlertTrend = await GetAlertTrendAsync(now.AddHours(-24), now, ct),
                RecordingTrend = await GetRecordingTrendAsync(now.AddHours(-24), now, ct)
            };
        }

        // ============================================================
        // CAMERA ANALYTICS
        // ============================================================
        public async Task<CameraAnalyticsSummaryDto> GetCameraSummaryAsync(Guid cameraId, CancellationToken ct = default)
        {
            var camera = await _db.Cameras.FindAsync(new object[] { cameraId }, ct);
            if (camera == null) throw new KeyNotFoundException("Camera not found");

            var todayStart = DateTime.UtcNow.Date;
            var todayRecordings = await _db.Recordings
                .Where(r => r.CameraId == cameraId && r.StartTime >= todayStart && !r.IsDeleted)
                .ToListAsync(ct);

            var todayAlerts = await _db.CameraEvents
                .Where(e => e.CameraId == cameraId && e.Timestamp >= todayStart)
                .ToListAsync(ct);

            var activeViewers = await _db.StreamSessions
                .CountAsync(s => s.CameraId == cameraId && s.IsActive, ct);

            return new CameraAnalyticsSummaryDto
            {
                CameraId = camera.Id,
                CameraName = camera.Name,
                Status = camera.Status,
                IsRecording = camera.IsRecording,
                UptimePercent = camera.IsOnline ? 100f : 0f,
                RecordingSeconds = todayRecordings.Sum(r => r.DurationSeconds),
                StorageBytesUsed = todayRecordings.Sum(r => r.FileSizeBytes),
                MotionEvents = todayAlerts.Count(e => e.EventType == "Motion"),
                ActiveViewers = activeViewers,
                LastSeenAt = camera.LastSeenAt
            };
        }

        // ============================================================
        // TRENDS
        // ============================================================
        public async Task<List<AlertTrendDto>> GetAlertTrendAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            var events = await _db.CameraEvents
                .Where(e => e.Timestamp >= from && e.Timestamp <= to)
                .ToListAsync(ct);

            return events
                .GroupBy(e => new DateTime(e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, e.Timestamp.Hour, 0, 0))
                .Select(g => new AlertTrendDto
                {
                    Hour = g.Key,
                    MotionCount = g.Count(e => e.EventType == "Motion"),
                    TamperCount = g.Count(e => e.EventType == "Tamper"),
                    ErrorCount = g.Count(e => e.Severity == "Alert"),
                    TotalCount = g.Count()
                })
                .OrderBy(t => t.Hour)
                .ToList();
        }

        public async Task<List<RecordingTrendDto>> GetRecordingTrendAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            var recordings = await _db.Recordings
                .Where(r => r.StartTime >= from && r.StartTime <= to && !r.IsDeleted)
                .ToListAsync(ct);

            return recordings
                .GroupBy(r => new DateTime(r.StartTime.Year, r.StartTime.Month, r.StartTime.Day, r.StartTime.Hour, 0, 0))
                .Select(g => new RecordingTrendDto
                {
                    Hour = g.Key,
                    RecordingCount = g.Count(),
                    TotalSeconds = g.Sum(r => r.DurationSeconds),
                    BytesWritten = g.Sum(r => r.FileSizeBytes)
                })
                .OrderBy(t => t.Hour)
                .ToList();
        }

        // ============================================================
        // UPTIME REPORT
        // ============================================================
        public async Task<CameraUptimeReportDto> GetUptimeReportAsync(Guid cameraId, DateTime from, DateTime to, CancellationToken ct = default)
        {
            var camera = await _db.Cameras.FindAsync(new object[] { cameraId }, ct);
            var events = await _db.CameraEvents
                .Where(e => e.CameraId == cameraId &&
                            e.Timestamp >= from &&
                            e.Timestamp <= to &&
                            (e.EventType == "Online" || e.EventType == "Offline"))
                .OrderBy(e => e.Timestamp)
                .ToListAsync(ct);

            var slots = new List<UptimeSlotDto>();
            var current = from;
            var isOnline = camera?.IsOnline ?? false;

            foreach (var ev in events)
            {
                slots.Add(new UptimeSlotDto
                {
                    Start = current,
                    End = ev.Timestamp,
                    Status = isOnline ? "Online" : "Offline"
                });
                current = ev.Timestamp;
                isOnline = ev.EventType == "Online";
            }

            slots.Add(new UptimeSlotDto { Start = current, End = to, Status = isOnline ? "Online" : "Offline" });

            var totalSeconds = (to - from).TotalSeconds;
            var onlineSeconds = slots.Where(s => s.Status == "Online").Sum(s => (s.End - s.Start).TotalSeconds);
            var offlineEvents = slots.Count(s => s.Status == "Offline");

            return new CameraUptimeReportDto
            {
                CameraId = cameraId,
                CameraName = camera?.Name ?? string.Empty,
                From = from,
                To = to,
                Slots = slots,
                OverallUptimePercent = totalSeconds > 0 ? (float)(onlineSeconds / totalSeconds * 100) : 0,
                TotalDowntimeMinutes = (int)(slots.Where(s => s.Status == "Offline").Sum(s => (s.End - s.Start).TotalMinutes)),
                TotalDowntimeEvents = offlineEvents
            };
        }

        // ============================================================
        // STORAGE HEATMAP
        // ============================================================
        public async Task<StorageHeatmapDto> GetStorageHeatmapAsync(Guid cameraId, int days = 30, CancellationToken ct = default)
        {
            var camera = await _db.Cameras.FindAsync(new object[] { cameraId }, ct);
            var from = DateTime.UtcNow.Date.AddDays(-days);

            var recordings = await _db.Recordings
                .Where(r => r.CameraId == cameraId && r.StartTime >= from && !r.IsDeleted)
                .ToListAsync(ct);

            var events = await _db.CameraEvents
                .Where(e => e.CameraId == cameraId && e.Timestamp >= from && e.EventType == "Motion")
                .ToListAsync(ct);

            var dayList = Enumerable.Range(0, days)
                .Select(i => DateTime.UtcNow.Date.AddDays(-days + i + 1))
                .ToList();

            return new StorageHeatmapDto
            {
                CameraId = cameraId,
                CameraName = camera?.Name ?? string.Empty,
                Days = dayList.Select(d => new StorageHeatmapDayDto
                {
                    Date = d,
                    BytesRecorded = recordings.Where(r => r.StartTime.Date == d).Sum(r => r.FileSizeBytes),
                    RecordingMinutes = recordings.Where(r => r.StartTime.Date == d).Sum(r => r.DurationSeconds) / 60,
                    MotionEvents = events.Count(e => e.Timestamp.Date == d),
                    HasRecording = recordings.Any(r => r.StartTime.Date == d)
                }).ToList()
            };
        }

        // ============================================================
        // VIEWER SESSION TRACKING
        // ============================================================
        public Task<int> GetActiveLiveViewersAsync(Guid? cameraId = null, CancellationToken ct = default)
        {
            var query = _db.StreamSessions.Where(s => s.IsActive && s.SessionType == "Live");
            if (cameraId.HasValue)
                query = query.Where(s => s.CameraId == cameraId);
            return query.CountAsync(ct);
        }

        public async Task RecordViewerSessionStartAsync(Guid cameraId, string userId, string connectionId, string clientIp, CancellationToken ct = default)
        {
            var session = new StreamSession
            {
                CameraId = cameraId,
                UserId = userId,
                ConnectionId = connectionId,
                ClientIp = clientIp,
                SessionType = "Live"
            };
            _db.StreamSessions.Add(session);
            await _db.SaveChangesAsync(ct);
        }

        public async Task RecordViewerSessionEndAsync(string connectionId, CancellationToken ct = default)
        {
            var sessions = await _db.StreamSessions
                .Where(s => s.ConnectionId == connectionId && s.IsActive)
                .ToListAsync(ct);

            foreach (var session in sessions)
            {
                session.IsActive = false;
                session.EndedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync(ct);
        }

        // ============================================================
        // HOURLY ANALYTICS SNAPSHOT
        // ============================================================
        public async Task TakeHourlySnapshotAsync(CancellationToken ct = default)
        {
            var hourStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, 0, 0);
            var cameras = await _db.Cameras.ToListAsync(ct);

            foreach (var camera in cameras)
            {
                var existing = await _db.CameraAnalyticsSnapshots
                    .FirstOrDefaultAsync(s => s.CameraId == camera.Id && s.Hour == hourStart, ct);

                if (existing != null) continue;

                var recordings = await _db.Recordings
                    .Where(r => r.CameraId == camera.Id &&
                                r.StartTime >= hourStart &&
                                r.StartTime < hourStart.AddHours(1) &&
                                !r.IsDeleted)
                    .ToListAsync(ct);

                var motionCount = await _db.CameraEvents
                    .CountAsync(e => e.CameraId == camera.Id &&
                                     e.Timestamp >= hourStart &&
                                     e.Timestamp < hourStart.AddHours(1) &&
                                     e.EventType == "Motion", ct);

                var peakViewers = await _db.StreamSessions
                    .CountAsync(s => s.CameraId == camera.Id &&
                                     s.StartedAt < hourStart.AddHours(1) &&
                                     (s.EndedAt == null || s.EndedAt > hourStart), ct);

                var snapshot = new CameraAnalyticsSnapshot
                {
                    CameraId = camera.Id,
                    Hour = hourStart,
                    UptimeSeconds = camera.IsOnline ? 3600 : 0,
                    RecordingSeconds = recordings.Sum(r => r.DurationSeconds),
                    StorageBytesWritten = recordings.Sum(r => r.FileSizeBytes),
                    MotionEventCount = motionCount,
                    ViewerCount = peakViewers,
                    AvgBitrateKbps = recordings.Any() ? (float)recordings.Average(r => r.BitrateKbps) : 0
                };

                _db.CameraAnalyticsSnapshots.Add(snapshot);
            }

            await _db.SaveChangesAsync(ct);
        }
    }
}
