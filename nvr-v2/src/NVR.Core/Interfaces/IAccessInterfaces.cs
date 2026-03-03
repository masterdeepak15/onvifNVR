using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NVR.Core.DTOs;
using NVR.Core.Entities;

namespace NVR.Core.Interfaces
{
    public interface ICameraAccessService
    {
        /// <summary>Check if a user has at minimum the given permission on a camera</summary>
        Task<bool> HasPermissionAsync(string userId, Guid cameraId, string requiredPermission, CancellationToken ct = default);

        /// <summary>Get all cameras a user can access with their effective permission level</summary>
        Task<List<CameraPermissionItem>> GetUserCameraPermissionsAsync(string userId, CancellationToken ct = default);

        /// <summary>Get all users who have access to a specific camera</summary>
        Task<List<CameraAccessDto>> GetCameraAccessListAsync(Guid cameraId, CancellationToken ct = default);

        /// <summary>Grant a user access to a camera</summary>
        Task<CameraAccessDto> GrantAccessAsync(Guid cameraId, string grantedByUserId, GrantCameraAccessRequest request, CancellationToken ct = default);

        /// <summary>Revoke a user's access to a camera</summary>
        Task RevokeAccessAsync(Guid accessId, CancellationToken ct = default);

        /// <summary>Update a user's permission level on a camera</summary>
        Task<CameraAccessDto> UpdateAccessAsync(Guid accessId, string permission, CancellationToken ct = default);

        /// <summary>Filter a list of camera IDs to only those accessible by a user</summary>
        Task<List<Guid>> FilterAccessibleCamerasAsync(string userId, IEnumerable<Guid> cameraIds, string requiredPermission = "View", CancellationToken ct = default);
    }

    // ============================================================
    // PERMISSION LEVELS (ordered by privilege)
    // ============================================================
    public static class CameraPermissions
    {
        public const string View = "View";       // Watch live + playback
        public const string Control = "Control"; // View + PTZ + snapshot
        public const string Record = "Record";   // Control + manual record start/stop
        public const string Admin = "Admin";     // Record + edit camera + delete recordings

        private static readonly string[] Ordered = { View, Control, Record, Admin };

        public static bool Includes(string userPermission, string requiredPermission)
        {
            var userLevel = Array.IndexOf(Ordered, userPermission);
            var reqLevel = Array.IndexOf(Ordered, requiredPermission);
            return userLevel >= reqLevel;
        }
    }

    // ============================================================
    // ANALYTICS SERVICE
    // ============================================================
    public interface IAnalyticsService
    {
        Task<NvrAnalyticsSummaryDto> GetSummaryAsync(CancellationToken ct = default);
        Task<CameraAnalyticsSummaryDto> GetCameraSummaryAsync(Guid cameraId, CancellationToken ct = default);
        Task<List<AlertTrendDto>> GetAlertTrendAsync(DateTime from, DateTime to, CancellationToken ct = default);
        Task<List<RecordingTrendDto>> GetRecordingTrendAsync(DateTime from, DateTime to, CancellationToken ct = default);
        Task<CameraUptimeReportDto> GetUptimeReportAsync(Guid cameraId, DateTime from, DateTime to, CancellationToken ct = default);
        Task<StorageHeatmapDto> GetStorageHeatmapAsync(Guid cameraId, int days = 30, CancellationToken ct = default);
        Task<int> GetActiveLiveViewersAsync(Guid? cameraId = null, CancellationToken ct = default);
        Task RecordViewerSessionStartAsync(Guid cameraId, string userId, string connectionId, string clientIp, CancellationToken ct = default);
        Task RecordViewerSessionEndAsync(string connectionId, CancellationToken ct = default);
        Task TakeHourlySnapshotAsync(CancellationToken ct = default);
    }

    // ============================================================
    // PLAYBACK SERVICE (multi-camera synchronized)
    // ============================================================
    public interface IPlaybackService
    {
        Task<string> CreateSessionAsync(string userId, string connectionId, IEnumerable<Guid> cameraIds, DateTime startPosition);
        Task<bool> PlayAsync(string sessionId, float speed = 1.0f);
        Task<bool> PauseAsync(string sessionId);
        Task<bool> StopAsync(string sessionId);
        Task<bool> SeekAsync(string sessionId, DateTime position);
        Task<bool> SetSpeedAsync(string sessionId, float speed);
        Task<PlaybackSession?> GetSessionAsync(string sessionId);
        Task CleanupSessionAsync(string sessionId);
    }
}
