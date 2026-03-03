using System;
using System.Collections.Generic;

namespace NVR.Core.Entities
{
    public class RecordingSchedule
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CameraId { get; set; }
        public Camera? Camera { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;

        // Days of week (bitmask: 1=Mon, 2=Tue, 4=Wed, 8=Thu, 16=Fri, 32=Sat, 64=Sun)
        public int DaysOfWeek { get; set; } = 127; // All days
        public TimeSpan StartTime { get; set; } = TimeSpan.Zero;
        public TimeSpan EndTime { get; set; } = TimeSpan.FromHours(24);
        public bool Continuous { get; set; } = true;  // If false, motion-only
        public string RecordingMode { get; set; } = "Continuous"; // Continuous, Motion, Schedule
        public int ChunkDurationSeconds { get; set; } = 60;
        public int BitrateKbps { get; set; } = 2000;
        public string Quality { get; set; } = "High"; // Low, Medium, High, Ultra

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class PtzPreset
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CameraId { get; set; }
        public Camera? Camera { get; set; }
        public string Name { get; set; } = string.Empty;
        public string OnvifToken { get; set; } = string.Empty; // ONVIF preset token
        public float? PanPosition { get; set; }
        public float? TiltPosition { get; set; }
        public float? ZoomPosition { get; set; }
        public string? ThumbnailPath { get; set; }
        public int OrderIndex { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CameraEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CameraId { get; set; }
        public Camera? Camera { get; set; }
        public string EventType { get; set; } = string.Empty; // Motion, Tamper, LinesCross, FaceDetect, Online, Offline
        public string Severity { get; set; } = "Info"; // Info, Warning, Alert
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Details { get; set; }
        public string? SnapshotPath { get; set; }
        public Guid? RecordingId { get; set; }
        public bool IsAcknowledged { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public string? AcknowledgedBy { get; set; }
    }

    public class UserCameraLayout
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } = string.Empty;
        public Guid CameraId { get; set; }
        public Camera? Camera { get; set; }
        public int GridPosition { get; set; }  // 0-63 (0-8 for 3x3, etc.)
        public int GridColumns { get; set; } = 4; // 2, 4, 8 columns
        public string LayoutName { get; set; } = "Default";
        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AppUser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "Viewer"; // Admin, Operator, Viewer
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }
        public ICollection<UserCameraLayout> CameraLayouts { get; set; } = new List<UserCameraLayout>();
    }

    public class SystemSetting
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; }
    }
}
