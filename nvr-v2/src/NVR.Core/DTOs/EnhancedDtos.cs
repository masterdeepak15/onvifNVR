using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NVR.Core.DTOs
{
    // ============================================================
    // CAMERA ACCESS / PERMISSIONS
    // ============================================================

    public class CameraAccessDto
    {
        public Guid Id { get; set; }
        public Guid CameraId { get; set; }
        public string CameraName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Permission { get; set; } = string.Empty;  // View | Control | Record | Admin
        public DateTime GrantedAt { get; set; }
        public string GrantedBy { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class GrantCameraAccessRequest
    {
        [Required] public string UserId { get; set; } = string.Empty;
        [Required] public string Permission { get; set; } = "View";  // View | Control | Record | Admin
        public DateTime? ExpiresAt { get; set; }
    }

    public class UserCameraPermissionsDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string GlobalRole { get; set; } = string.Empty;  // Admin | Operator | Viewer
        public bool IsAdmin { get; set; }
        public List<CameraPermissionItem> CameraPermissions { get; set; } = new();
    }

    public class CameraPermissionItem
    {
        public Guid CameraId { get; set; }
        public string CameraName { get; set; } = string.Empty;
        public string Permission { get; set; } = string.Empty;
        public bool IsExplicit { get; set; }  // true = explicit grant, false = inherited from Admin role
    }

    // ============================================================
    // STREAM CONTROL COMMANDS (via SignalR)
    // ============================================================

    /// <summary>All NVR playback/stream controls sent from client → server over SignalR</summary>
    public class StreamControlCommand
    {
        [Required] public Guid CameraId { get; set; }
        [Required] public string Command { get; set; } = string.Empty;
        // Commands: Play, Pause, Stop, Resume, SetSpeed, Seek, ZoomIn, ZoomOut, ZoomReset
        public float? Speed { get; set; }       // For SetSpeed: 0.25, 0.5, 1, 2, 4, 8
        public DateTime? SeekTo { get; set; }   // For Seek: target timestamp
        public float? ZoomLevel { get; set; }   // For zoom: 0.0 - 1.0
    }

    public class StreamStateDto
    {
        public Guid CameraId { get; set; }
        public string State { get; set; } = string.Empty;  // Live, Playing, Paused, Stopped, Buffering, Error
        public float Speed { get; set; } = 1.0f;
        public float ZoomLevel { get; set; } = 0.0f;
        public DateTime? PlaybackPosition { get; set; }   // null = live stream
        public bool IsLive { get; set; } = true;
        public string? ErrorMessage { get; set; }
        public int Fps { get; set; }
        public int BitrateKbps { get; set; }
        public int BufferedMs { get; set; }
    }

    // ============================================================
    // PTZ ENHANCED COMMANDS (via SignalR)
    // ============================================================

    public class PtzCommandDto
    {
        [Required] public Guid CameraId { get; set; }
        [Required] public string Action { get; set; } = string.Empty;
        // Actions: MoveUp, MoveDown, MoveLeft, MoveRight, MoveUpLeft, MoveUpRight, MoveDownLeft, MoveDownRight,
        //          ZoomIn, ZoomOut, Stop, GoToPreset, SavePreset, DeletePreset,
        //          FocusNear, FocusFar, FocusAuto,
        //          IrisOpen, IrisClose, IrisAuto,
        //          AbsoluteMove, RelativeMove, ContinuousMove, Home
        public float Speed { get; set; } = 0.5f;   // 0.0 - 1.0 movement speed
        public float Pan { get; set; }              // -1.0 to 1.0
        public float Tilt { get; set; }             // -1.0 to 1.0
        public float Zoom { get; set; }             // 0.0 to 1.0
        public string? PresetToken { get; set; }    // For GoToPreset / DeletePreset
        public string? PresetName { get; set; }     // For SavePreset
    }

    public class PtzStatusDto
    {
        public Guid CameraId { get; set; }
        public float Pan { get; set; }
        public float Tilt { get; set; }
        public float Zoom { get; set; }
        public string MoveStatus { get; set; } = "Idle";  // Idle, Moving, Unknown
        public bool SupportsFocus { get; set; }
        public bool SupportsIris { get; set; }
        public List<PtzPresetDto> Presets { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    // ============================================================
    // ANALYTICS DTOs
    // ============================================================

    public class NvrAnalyticsSummaryDto
    {
        // System Health
        public int TotalCameras { get; set; }
        public int OnlineCameras { get; set; }
        public int OfflineCameras { get; set; }
        public int RecordingCameras { get; set; }
        public float SystemUptimePercent { get; set; }

        // Storage
        public long TotalStorageBytes { get; set; }
        public long UsedStorageBytes { get; set; }
        public double StorageUsagePercent { get; set; }
        public long StorageBytesWrittenToday { get; set; }
        public int EstimatedDaysRemaining { get; set; }

        // Recording
        public long TotalRecordingHoursToday { get; set; }
        public long TotalRecordingHoursWeek { get; set; }
        public int TotalRecordingsToday { get; set; }
        public int ActiveRecordings { get; set; }

        // Events & Alerts
        public int TotalAlertsToday { get; set; }
        public int UnacknowledgedAlerts { get; set; }
        public int MotionEventsToday { get; set; }
        public int CameraErrorsToday { get; set; }

        // Streaming
        public int ActiveLiveViewers { get; set; }
        public int ActivePlaybackSessions { get; set; }
        public int PeakViewersToday { get; set; }

        // Per-camera breakdown
        public List<CameraAnalyticsSummaryDto> CameraBreakdown { get; set; } = new();
        public List<StorageAnalyticsDto> StorageBreakdown { get; set; } = new();
        public List<AlertTrendDto> AlertTrend { get; set; } = new();
        public List<RecordingTrendDto> RecordingTrend { get; set; } = new();
    }

    public class CameraAnalyticsSummaryDto
    {
        public Guid CameraId { get; set; }
        public string CameraName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsRecording { get; set; }
        public float UptimePercent { get; set; }
        public long RecordingSeconds { get; set; }
        public long StorageBytesUsed { get; set; }
        public int MotionEvents { get; set; }
        public int ActiveViewers { get; set; }
        public DateTime? LastMotionAt { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public float AvgBitrateKbps { get; set; }
    }

    public class StorageAnalyticsDto
    {
        public Guid ProfileId { get; set; }
        public string ProfileName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long TotalBytes { get; set; }
        public long UsedBytes { get; set; }
        public double UsagePercent { get; set; }
        public int RetentionDays { get; set; }
        public int EstimatedDaysRemaining { get; set; }
        public bool IsHealthy { get; set; }
        public long BytesWrittenToday { get; set; }
    }

    public class AlertTrendDto
    {
        public DateTime Hour { get; set; }
        public int MotionCount { get; set; }
        public int TamperCount { get; set; }
        public int ErrorCount { get; set; }
        public int TotalCount { get; set; }
    }

    public class RecordingTrendDto
    {
        public DateTime Hour { get; set; }
        public int RecordingCount { get; set; }
        public long TotalSeconds { get; set; }
        public long BytesWritten { get; set; }
    }

    public class CameraUptimeReportDto
    {
        public Guid CameraId { get; set; }
        public string CameraName { get; set; } = string.Empty;
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<UptimeSlotDto> Slots { get; set; } = new();
        public float OverallUptimePercent { get; set; }
        public int TotalDowntimeMinutes { get; set; }
        public int TotalDowntimeEvents { get; set; }
    }

    public class UptimeSlotDto
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Status { get; set; } = string.Empty;  // Online, Offline, Error
    }

    public class StorageHeatmapDto
    {
        public Guid CameraId { get; set; }
        public string CameraName { get; set; } = string.Empty;
        public List<StorageHeatmapDayDto> Days { get; set; } = new();
    }

    public class StorageHeatmapDayDto
    {
        public DateTime Date { get; set; }
        public long BytesRecorded { get; set; }
        public int RecordingMinutes { get; set; }
        public int MotionEvents { get; set; }
        public bool HasRecording { get; set; }
    }

    // ============================================================
    // LIVE VIEWER TRACKING
    // ============================================================

    public class LiveViewerDto
    {
        public Guid CameraId { get; set; }
        public int ViewerCount { get; set; }
        public List<ViewerInfoDto> Viewers { get; set; } = new();
    }

    public class ViewerInfoDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; set; }
        public string ClientIp { get; set; } = string.Empty;
    }

    // ============================================================
    // SIGNALR EVENT PAYLOADS (Server → Client)
    // ============================================================

    public class CameraFramePayload
    {
        public Guid CameraId { get; set; }
        public string Frame { get; set; } = string.Empty;  // Base64 JPEG
        public long TimestampMs { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Fps { get; set; }
        public bool IsKeyframe { get; set; }
        public string StreamState { get; set; } = "Live";  // Live | Playback
    }

    public class CameraStatusPayload
    {
        public Guid CameraId { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public bool IsRecording { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public string? LastError { get; set; }
        public int ActiveViewers { get; set; }
    }

    public class AlertPayload
    {
        public Guid AlertId { get; set; }
        public Guid? CameraId { get; set; }
        public string? CameraName { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;  // Info | Warning | Critical
        public DateTime Timestamp { get; set; }
        public string? SnapshotBase64 { get; set; }
    }

    public class PtzFeedbackPayload
    {
        public Guid CameraId { get; set; }
        public string Action { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
        // PTZ position (present for all PTZ actions)
        public float Pan { get; set; }
        public float Tilt { get; set; }
        public float Zoom { get; set; }
        public string MoveStatus { get; set; } = "Idle";
        // Focus state (populated for Focus* actions; null for PTZ/Iris actions)
        public float? FocusPosition { get; set; }
        public string? FocusMode { get; set; }      // "Auto" | "Manual" | null
        public string? FocusMoveStatus { get; set; } // "IDLE" | "MOVING" | "UNKNOWN" | null
        // Iris/exposure state (populated for Iris* actions; null for PTZ/Focus actions)
        public float? IrisLevel { get; set; }
        public string? IrisMode { get; set; }       // "Auto" | "Manual" | null
    }


    public class RecordingStatusPayload
    {
        public Guid CameraId { get; set; }
        public Guid? RecordingId { get; set; }
        public string Status { get; set; } = string.Empty;  // Started | Stopped | Chunk | Error
        public int? ChunkNumber { get; set; }
        public long? FileSizeBytes { get; set; }
        public int? DurationSeconds { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class AnalyticsUpdatePayload
    {
        public int OnlineCameras { get; set; }
        public int RecordingCameras { get; set; }
        public int ActiveViewers { get; set; }
        public double StorageUsagePercent { get; set; }
        public int UnacknowledgedAlerts { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
