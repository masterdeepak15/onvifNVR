using System;
using System.Collections.Generic;

namespace NVR.Core.Entities
{
    // ============================================================
    // ROLE-BASED CAMERA ACCESS
    // ============================================================

    /// <summary>
    /// Grants specific users access to specific cameras beyond their global role.
    /// Admins have access to all cameras by default.
    /// Operators and Viewers need explicit grants per camera.
    /// </summary>
    public class CameraUserAccess
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CameraId { get; set; }
        public Camera? Camera { get; set; }
        public string UserId { get; set; } = string.Empty;
        public AppUser? User { get; set; }

        /// <summary>
        /// Permission level: View, Control, Record, Admin
        /// View    - Live view + playback only
        /// Control - View + PTZ control + snapshot
        /// Record  - Control + start/stop recording manually
        /// Admin   - Full access (edit camera, delete recordings, etc.)
        /// </summary>
        public string Permission { get; set; } = "View";

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
        public string GrantedBy { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }  // Optional time-limited access
        public bool IsActive { get; set; } = true;
    }

    // ============================================================
    // LIVE STREAM SESSION TRACKING
    // ============================================================

    /// <summary>
    /// Tracks active SignalR streaming sessions per client
    /// Used for audit, analytics, and resource management
    /// </summary>
    public class StreamSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CameraId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;  // SignalR connection ID
        public string ClientIp { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndedAt { get; set; }
        public long BytesStreamed { get; set; }
        public int FramesSent { get; set; }
        public string SessionType { get; set; } = "Live";  // Live, Playback
        public bool IsActive { get; set; } = true;
    }

    // ============================================================
    // NVR ANALYTICS SNAPSHOTS (hourly aggregates)
    // ============================================================

    /// <summary>
    /// Hourly analytics snapshot per camera for dashboard metrics
    /// Written by background service every hour
    /// </summary>
    public class CameraAnalyticsSnapshot
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CameraId { get; set; }
        public Camera? Camera { get; set; }

        public DateTime Hour { get; set; }          // Truncated to the hour
        public int UptimeSeconds { get; set; }       // Seconds camera was online this hour
        public int RecordingSeconds { get; set; }    // Seconds of recording this hour
        public long StorageBytesWritten { get; set; } // Bytes recorded this hour
        public int MotionEventCount { get; set; }    // Number of motion detections
        public int ErrorCount { get; set; }          // Number of errors
        public int ViewerCount { get; set; }         // Peak concurrent viewers
        public float AvgBitrateKbps { get; set; }   // Average recording bitrate
        public float UptimePercent => UptimeSeconds > 0 ? (float)UptimeSeconds / 3600 * 100 : 0;
    }

    /// <summary>
    /// System-wide analytics snapshot (daily)
    /// </summary>
    public class SystemAnalyticsSnapshot
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Date { get; set; }                     // Day (UTC)
        public int TotalCameras { get; set; }
        public int OnlineCameras { get; set; }
        public int RecordingCameras { get; set; }
        public long TotalRecordingSeconds { get; set; }
        public long TotalStorageBytesWritten { get; set; }
        public long TotalStorageBytesDeleted { get; set; }
        public int TotalMotionEvents { get; set; }
        public int TotalViewerSessions { get; set; }
        public int PeakConcurrentViewers { get; set; }
        public int TotalAlerts { get; set; }
        public int AcknowledgedAlerts { get; set; }
        public double StorageUsagePercent { get; set; }
    }

    // ============================================================
    // PLAYBACK SESSION (multi-camera sync playback state)
    // ============================================================

    public class PlaybackSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public List<Guid> CameraIds { get; set; } = new();
        public DateTime PlaybackPosition { get; set; }    // Current position in recording time
        public float PlaybackSpeed { get; set; } = 1.0f;  // 0.25, 0.5, 1, 2, 4, 8x
        public string PlaybackState { get; set; } = "Paused"; // Playing, Paused, Stopped
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    }
}
