using System;
using System.Collections.Generic;

namespace NVR.Core.Entities
{
    public class Camera
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 80;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string RtspUrl { get; set; } = string.Empty;          // ONVIF discovered or manual
        public string OnvifServiceUrl { get; set; } = string.Empty;  // ONVIF device service URL
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string FirmwareVersion { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public bool IsRecording { get; set; }
        public bool PtzCapable { get; set; }
        public bool AudioEnabled { get; set; }
        public int Resolution_Width { get; set; } = 1920;
        public int Resolution_Height { get; set; } = 1080;
        public int Framerate { get; set; } = 25;
        public string Codec { get; set; } = "H264";
        public string Status { get; set; } = "Unknown"; // Online, Offline, Error
        public string? LastError { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastSeenAt { get; set; }

        // User layout position
        public int GridPosition { get; set; } = -1;  // -1 = unassigned

        // Foreign keys
        public Guid? StorageProfileId { get; set; }
        public StorageProfile? StorageProfile { get; set; }

        // Relations
        public ICollection<RecordingSchedule> RecordingSchedules { get; set; } = new List<RecordingSchedule>();
        public ICollection<Recording> Recordings { get; set; } = new List<Recording>();
        public ICollection<PtzPreset> PtzPresets { get; set; } = new List<PtzPreset>();
        public ICollection<CameraEvent> Events { get; set; } = new List<CameraEvent>();
        public ICollection<UserCameraLayout> UserLayouts { get; set; } = new List<UserCameraLayout>();
    }
}
