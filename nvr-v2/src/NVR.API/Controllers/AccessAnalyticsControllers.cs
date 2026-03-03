using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NVR.Core.DTOs;
using NVR.Core.Interfaces;
using NVR.Infrastructure.Data;
using NVR.Infrastructure.Services;

namespace NVR.API.Controllers
{
    // ============================================================
    // CAMERA ACCESS / ROLE-BASED PERMISSION CONTROLLER
    // ============================================================

    [ApiController, Route("api/cameras/{cameraId}/access"), Authorize]
    public class CameraAccessController : ControllerBase
    {
        private readonly ICameraAccessService _accessService;
        private readonly NvrDbContext _db;

        public CameraAccessController(ICameraAccessService accessService, NvrDbContext db)
        {
            _accessService = accessService;
            _db = db;
        }

        /// <summary>List all users who have access to a camera (Admin only)</summary>
        [HttpGet, Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<CameraAccessDto>>> GetAccessList(Guid cameraId)
            => Ok(await _accessService.GetCameraAccessListAsync(cameraId));

        /// <summary>Grant a user access to this camera (Admin only)</summary>
        [HttpPost, Authorize(Roles = "Admin")]
        public async Task<ActionResult<CameraAccessDto>> GrantAccess(Guid cameraId, [FromBody] GrantCameraAccessRequest request)
        {
            var granterId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            try
            {
                var result = await _accessService.GrantAccessAsync(cameraId, granterId, request);
                return Ok(result);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Update permission level for an existing access grant (Admin only)</summary>
        [HttpPut("{accessId}"), Authorize(Roles = "Admin")]
        public async Task<ActionResult<CameraAccessDto>> UpdateAccess(Guid cameraId, Guid accessId, [FromBody] string permission)
        {
            try { return Ok(await _accessService.UpdateAccessAsync(accessId, permission)); }
            catch (KeyNotFoundException) { return NotFound(); }
        }

        /// <summary>Revoke a user's camera access (Admin only)</summary>
        [HttpDelete("{accessId}"), Authorize(Roles = "Admin")]
        public async Task<IActionResult> RevokeAccess(Guid cameraId, Guid accessId)
        {
            await _accessService.RevokeAccessAsync(accessId);
            return NoContent();
        }

        /// <summary>Check current user's permission on a specific camera</summary>
        [HttpGet("my-permission")]
        public async Task<ActionResult<object>> GetMyPermission(Guid cameraId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var canView = await _accessService.HasPermissionAsync(userId, cameraId, CameraPermissions.View);
            var canControl = await _accessService.HasPermissionAsync(userId, cameraId, CameraPermissions.Control);
            var canRecord = await _accessService.HasPermissionAsync(userId, cameraId, CameraPermissions.Record);
            var canAdmin = await _accessService.HasPermissionAsync(userId, cameraId, CameraPermissions.Admin);

            string level = canAdmin ? CameraPermissions.Admin
                : canRecord ? CameraPermissions.Record
                : canControl ? CameraPermissions.Control
                : canView ? CameraPermissions.View
                : "None";

            return Ok(new
            {
                CameraId = cameraId,
                Permission = level,
                CanView = canView,
                CanControl = canControl,
                CanRecord = canRecord,
                CanAdmin = canAdmin
            });
        }
    }

    // ============================================================
    // USER PERMISSIONS OVERVIEW CONTROLLER
    // ============================================================

    [ApiController, Route("api/users/{userId}/camera-permissions"), Authorize(Roles = "Admin")]
    public class UserCameraPermissionsController : ControllerBase
    {
        private readonly ICameraAccessService _accessService;
        private readonly NvrDbContext _db;

        public UserCameraPermissionsController(ICameraAccessService accessService, NvrDbContext db)
        {
            _accessService = accessService;
            _db = db;
        }

        /// <summary>Get all camera permissions for a specific user</summary>
        [HttpGet]
        public async Task<ActionResult<UserCameraPermissionsDto>> GetUserPermissions(string userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var permissions = await _accessService.GetUserCameraPermissionsAsync(userId);

            return Ok(new UserCameraPermissionsDto
            {
                UserId = user.Id,
                Username = user.Username,
                GlobalRole = user.Role,
                IsAdmin = user.Role == "Admin",
                CameraPermissions = permissions
            });
        }

        /// <summary>Bulk grant/update camera access for a user</summary>
        [HttpPut("bulk")]
        public async Task<IActionResult> BulkUpdatePermissions(string userId, [FromBody] List<CameraPermissionItem> permissions)
        {
            var granterId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            foreach (var perm in permissions)
            {
                await _accessService.GrantAccessAsync(perm.CameraId, granterId, new GrantCameraAccessRequest
                {
                    UserId = userId,
                    Permission = perm.Permission
                });
            }
            return Ok(new { message = $"Updated {permissions.Count} camera permissions" });
        }
    }

    // ============================================================
    // ANALYTICS CONTROLLER — NVR-SPECIFIC METHODS
    // ============================================================

    [ApiController, Route("api/analytics"), Authorize]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analytics;
        private readonly NvrDbContext _db;
        private readonly ICameraAccessService _accessService;

        public AnalyticsController(IAnalyticsService analytics, NvrDbContext db, ICameraAccessService accessService)
        {
            _analytics = analytics;
            _db = db;
            _accessService = accessService;
        }

        /// <summary>Full NVR dashboard analytics summary</summary>
        [HttpGet("summary")]
        public async Task<ActionResult<NvrAnalyticsSummaryDto>> GetSummary()
        {
            var summary = await _analytics.GetSummaryAsync();

            // Filter camera breakdown to only accessible cameras for non-admins
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if (!User.IsInRole("Admin"))
            {
                var accessibleIds = await _accessService.FilterAccessibleCamerasAsync(
                    userId,
                    summary.CameraBreakdown.Select(c => c.CameraId));
                summary.CameraBreakdown = summary.CameraBreakdown
                    .Where(c => accessibleIds.Contains(c.CameraId)).ToList();
            }

            return Ok(summary);
        }

        /// <summary>Per-camera analytics (requires View access)</summary>
        [HttpGet("cameras/{cameraId}")]
        public async Task<ActionResult<CameraAnalyticsSummaryDto>> GetCameraAnalytics(Guid cameraId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if (!User.IsInRole("Admin") && !await _accessService.HasPermissionAsync(userId, cameraId, CameraPermissions.View))
                return Forbid();

            return Ok(await _analytics.GetCameraSummaryAsync(cameraId));
        }

        /// <summary>Alert trend (hourly counts) for a time range</summary>
        [HttpGet("alerts/trend")]
        public async Task<ActionResult<List<AlertTrendDto>>> GetAlertTrend(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;
            return Ok(await _analytics.GetAlertTrendAsync(from.Value, to.Value));
        }

        /// <summary>Recording trend (hourly stats) for a time range</summary>
        [HttpGet("recordings/trend")]
        public async Task<ActionResult<List<RecordingTrendDto>>> GetRecordingTrend(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;
            return Ok(await _analytics.GetRecordingTrendAsync(from.Value, to.Value));
        }

        /// <summary>Camera uptime report for a time range</summary>
        [HttpGet("cameras/{cameraId}/uptime")]
        public async Task<ActionResult<CameraUptimeReportDto>> GetUptimeReport(
            Guid cameraId,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if (!User.IsInRole("Admin") && !await _accessService.HasPermissionAsync(userId, cameraId, CameraPermissions.View))
                return Forbid();

            from ??= DateTime.UtcNow.AddDays(-30);
            to ??= DateTime.UtcNow;
            return Ok(await _analytics.GetUptimeReportAsync(cameraId, from.Value, to.Value));
        }

        /// <summary>Storage heatmap — recording activity per day</summary>
        [HttpGet("cameras/{cameraId}/heatmap")]
        public async Task<ActionResult<StorageHeatmapDto>> GetStorageHeatmap(
            Guid cameraId,
            [FromQuery] int days = 30)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if (!User.IsInRole("Admin") && !await _accessService.HasPermissionAsync(userId, cameraId, CameraPermissions.View))
                return Forbid();

            return Ok(await _analytics.GetStorageHeatmapAsync(cameraId, days));
        }

        /// <summary>Live viewer counts per camera</summary>
        [HttpGet("viewers")]
        public async Task<ActionResult<object>> GetLiveViewers([FromQuery] Guid? cameraId = null)
        {
            var count = await _analytics.GetActiveLiveViewersAsync(cameraId);
            return Ok(new { Count = count, CameraId = cameraId, Timestamp = DateTime.UtcNow });
        }

        /// <summary>Storage usage breakdown across all profiles</summary>
        [HttpGet("storage"), Authorize(Roles = "Admin,Operator")]
        public async Task<ActionResult<object>> GetStorageStats()
        {
            var profiles = await _db.StorageProfiles.Where(s => s.IsEnabled).ToListAsync();
            return Ok(profiles.Select(s => new StorageAnalyticsDto
            {
                ProfileId = s.Id,
                ProfileName = s.Name,
                Type = s.Type,
                TotalBytes = s.MaxStorageBytes,
                UsedBytes = s.UsedStorageBytes,
                UsagePercent = s.MaxStorageBytes > 0 ? (double)s.UsedStorageBytes / s.MaxStorageBytes * 100 : 0,
                RetentionDays = s.RetentionDays,
                IsHealthy = s.IsHealthy
            }));
        }

        /// <summary>Recent event log with camera name (for dashboard activity feed)</summary>
        [HttpGet("events/recent")]
        public async Task<ActionResult<object>> GetRecentEvents(
            [FromQuery] int count = 100,
            [FromQuery] string? severity = null,
            [FromQuery] string? eventType = null,
            [FromQuery] bool unacknowledgedOnly = false)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var query = _db.CameraEvents.Include(e => e.Camera).AsQueryable();

            if (severity != null) query = query.Where(e => e.Severity == severity);
            if (eventType != null) query = query.Where(e => e.EventType == eventType);
            if (unacknowledgedOnly) query = query.Where(e => !e.IsAcknowledged);

            var events = await query
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToListAsync();

            // Filter to accessible cameras for non-admins
            if (!User.IsInRole("Admin"))
            {
                var cameraIds = events.Select(e => e.CameraId).Distinct().ToList();
                var accessible = await _accessService.FilterAccessibleCamerasAsync(userId, cameraIds);
                events = events.Where(e => accessible.Contains(e.CameraId)).ToList();
            }

            return Ok(events.Select(e => new
            {
                e.Id,
                e.CameraId,
                CameraName = e.Camera?.Name,
                e.EventType,
                e.Severity,
                e.Timestamp,
                e.Details,
                e.IsAcknowledged,
                e.AcknowledgedAt,
                e.AcknowledgedBy
            }));
        }

        /// <summary>Acknowledge events in bulk</summary>
        [HttpPost("events/acknowledge")]
        public async Task<IActionResult> BulkAcknowledge([FromBody] List<Guid> eventIds)
        {
            var username = User.FindFirstValue(ClaimTypes.Name)!;
            var events = await _db.CameraEvents.Where(e => eventIds.Contains(e.Id)).ToListAsync();

            foreach (var ev in events)
            {
                ev.IsAcknowledged = true;
                ev.AcknowledgedAt = DateTime.UtcNow;
                ev.AcknowledgedBy = username;
            }

            await _db.SaveChangesAsync();
            return Ok(new { AcknowledgedCount = events.Count });
        }

        /// <summary>Recording statistics by camera for a date range</summary>
        [HttpGet("recordings/by-camera")]
        public async Task<ActionResult<object>> GetRecordingsByCamera(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            from ??= DateTime.UtcNow.Date;
            to ??= DateTime.UtcNow;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var query = _db.Recordings
                .Include(r => r.Camera)
                .Where(r => r.StartTime >= from && r.StartTime <= to && !r.IsDeleted);

            var recordings = await query.ToListAsync();

            // Filter accessible
            if (!User.IsInRole("Admin"))
            {
                var camIds = recordings.Select(r => r.CameraId).Distinct().ToList();
                var accessible = await _accessService.FilterAccessibleCamerasAsync(userId, camIds);
                recordings = recordings.Where(r => accessible.Contains(r.CameraId)).ToList();
            }

            return Ok(recordings
                .GroupBy(r => new { r.CameraId, CameraName = r.Camera?.Name ?? "Unknown" })
                .Select(g => new
                {
                    g.Key.CameraId,
                    g.Key.CameraName,
                    RecordingCount = g.Count(),
                    TotalSeconds = g.Sum(r => r.DurationSeconds),
                    TotalHours = Math.Round(g.Sum(r => r.DurationSeconds) / 3600.0, 2),
                    TotalSizeBytes = g.Sum(r => r.FileSizeBytes),
                    TotalSizeGB = Math.Round(g.Sum(r => r.FileSizeBytes) / (1024.0 * 1024 * 1024), 3),
                    MotionTriggered = g.Count(r => r.TriggerType == "Motion"),
                    Scheduled = g.Count(r => r.TriggerType == "Scheduled")
                }));
        }

        /// <summary>System health check — status of all cameras and storage</summary>
        [HttpGet("health"), Authorize(Roles = "Admin,Operator")]
        public async Task<ActionResult<object>> GetSystemHealth()
        {
            var cameras = await _db.Cameras.ToListAsync();
            var storage = await _db.StorageProfiles.Where(s => s.IsEnabled).ToListAsync();

            var unhealthyCameras = cameras.Where(c => !c.IsOnline).Select(c => new
            {
                c.Id, c.Name, c.Status, c.LastError, c.LastSeenAt
            }).ToList();

            var unhealthyStorage = storage.Where(s => !s.IsHealthy).Select(s => new
            {
                s.Id, s.Name, s.Type, s.HealthError, s.LastHealthCheck
            }).ToList();

            var warningStorage = storage.Where(s =>
                s.MaxStorageBytes > 0 &&
                (double)s.UsedStorageBytes / s.MaxStorageBytes * 100 > s.LowSpaceWarningPercent)
                .Select(s => new
                {
                    s.Id, s.Name,
                    UsagePercent = Math.Round((double)s.UsedStorageBytes / s.MaxStorageBytes * 100, 1)
                }).ToList();

            return Ok(new
            {
                OverallStatus = unhealthyCameras.Any() || unhealthyStorage.Any() ? "Degraded" : "Healthy",
                TotalCameras = cameras.Count,
                OnlineCameras = cameras.Count(c => c.IsOnline),
                OfflineCameras = cameras.Count(c => !c.IsOnline),
                RecordingCameras = cameras.Count(c => c.IsRecording),
                UnhealthyCameras = unhealthyCameras,
                StorageProfiles = storage.Count,
                UnhealthyStorage = unhealthyStorage,
                LowSpaceStorage = warningStorage,
                CheckedAt = DateTime.UtcNow
            });
        }

        /// <summary>Motion activity summary — most active cameras</summary>
        [HttpGet("motion/summary")]
        public async Task<ActionResult<object>> GetMotionSummary([FromQuery] int hours = 24)
        {
            var from = DateTime.UtcNow.AddHours(-hours);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var motionEvents = await _db.CameraEvents
                .Include(e => e.Camera)
                .Where(e => e.EventType == "Motion" && e.Timestamp >= from)
                .ToListAsync();

            if (!User.IsInRole("Admin"))
            {
                var camIds = motionEvents.Select(e => e.CameraId).Distinct().ToList();
                var accessible = await _accessService.FilterAccessibleCamerasAsync(userId, camIds);
                motionEvents = motionEvents.Where(e => accessible.Contains(e.CameraId)).ToList();
            }

            return Ok(new
            {
                TotalEvents = motionEvents.Count,
                PeriodHours = hours,
                MostActiveCamera = motionEvents
                    .GroupBy(e => new { e.CameraId, CameraName = e.Camera?.Name ?? "Unknown" })
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key,
                ByCamera = motionEvents
                    .GroupBy(e => new { e.CameraId, CameraName = e.Camera?.Name ?? "Unknown" })
                    .Select(g => new
                    {
                        g.Key.CameraId,
                        g.Key.CameraName,
                        Count = g.Count(),
                        LastAt = g.Max(e => e.Timestamp)
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList(),
                HourlyBreakdown = motionEvents
                    .GroupBy(e => new DateTime(e.Timestamp.Year, e.Timestamp.Month, e.Timestamp.Day, e.Timestamp.Hour, 0, 0))
                    .Select(g => new { Hour = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Hour)
                    .ToList()
            });
        }

        /// <summary>Bandwidth / bitrate stats per camera</summary>
        [HttpGet("bandwidth")]
        public async Task<ActionResult<object>> GetBandwidthStats()
        {
            var recentChunks = await _db.RecordingChunks
                .Include(c => c.Recording)
                .ThenInclude(r => r!.Camera)
                .Where(c => c.StartTime >= DateTime.UtcNow.AddHours(-1))
                .ToListAsync();

            return Ok(recentChunks
                .GroupBy(c => new { c.CameraId, CameraName = c.Recording?.Camera?.Name ?? "Unknown" })
                .Select(g => new
                {
                    g.Key.CameraId,
                    g.Key.CameraName,
                    ChunkCount = g.Count(),
                    TotalBytes = g.Sum(c => c.FileSizeBytes),
                    AvgBytesPerChunk = g.Average(c => (double)c.FileSizeBytes),
                    EstimatedBitrateKbps = (int)(g.Sum(c => c.FileSizeBytes) * 8 / 1024.0 /
                                                  Math.Max(g.Sum(c => c.DurationMs) / 1000.0, 1))
                }));
        }
    }

    // ============================================================
    // MY CAMERAS CONTROLLER (Viewer-friendly: only shows accessible cameras)
    // ============================================================

    [ApiController, Route("api/my"), Authorize]
    public class MyCamerasController : ControllerBase
    {
        private readonly ICameraAccessService _accessService;
        private readonly NvrDbContext _db;

        public MyCamerasController(ICameraAccessService accessService, NvrDbContext db)
        {
            _accessService = accessService;
            _db = db;
        }

        /// <summary>Get all cameras this user can access, with their permission level</summary>
        [HttpGet("cameras")]
        public async Task<ActionResult<List<object>>> GetMyCameras()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var permissions = await _accessService.GetUserCameraPermissionsAsync(userId);

            var cameraIds = permissions.Select(p => p.CameraId).ToList();
            var cameras = await _db.Cameras
                .Include(c => c.PtzPresets)
                .Where(c => cameraIds.Contains(c.Id))
                .ToListAsync();

            return Ok(cameras.Select(c =>
            {
                var perm = permissions.FirstOrDefault(p => p.CameraId == c.Id);
                return new
                {
                    c.Id, c.Name, c.IpAddress, c.Status, c.IsOnline, c.IsRecording,
                    c.PtzCapable, c.Resolution_Width, c.Resolution_Height, c.Framerate,
                    c.GridPosition, c.LastSeenAt,
                    Permission = perm?.Permission ?? "View",
                    CanControl = CameraPermissions.Includes(perm?.Permission ?? "View", CameraPermissions.Control),
                    CanRecord = CameraPermissions.Includes(perm?.Permission ?? "View", CameraPermissions.Record),
                    PtzPresets = c.PtzPresets.Select(p => new { p.Id, p.Name, p.OnvifToken })
                };
            }));
        }

        /// <summary>Get my layout (only positions for cameras I can access)</summary>
        [HttpGet("layout")]
        public async Task<ActionResult<object>> GetMyLayout([FromQuery] string layoutName = "Default")
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var permissions = await _accessService.GetUserCameraPermissionsAsync(userId);
            var accessibleIds = permissions.Select(p => p.CameraId).ToHashSet();

            var layouts = await _db.UserCameraLayouts
                .Where(l => l.UserId == userId && l.LayoutName == layoutName)
                .ToListAsync();

            return Ok(new
            {
                LayoutName = layoutName,
                GridColumns = layouts.FirstOrDefault()?.GridColumns ?? 4,
                Positions = layouts
                    .Where(l => accessibleIds.Contains(l.CameraId))
                    .Select(l => new { l.CameraId, l.GridPosition })
                    .ToList()
            });
        }
    }
}
