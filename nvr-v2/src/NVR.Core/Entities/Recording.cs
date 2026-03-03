using System;

namespace NVR.Core.Entities
{
    public class Recording
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CameraId { get; set; }
        public Camera? Camera { get; set; }
        public Guid StorageProfileId { get; set; }
        public StorageProfile? StorageProfile { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public long FileSizeBytes { get; set; }
        public int DurationSeconds { get; set; }
        public string StoragePath { get; set; } = string.Empty;  // Relative path in storage
        public string IndexPath { get; set; } = string.Empty;    // Path to chunk index JSON
        public string ThumbnailPath { get; set; } = string.Empty;

        public string Status { get; set; } = "Recording"; // Recording, Completed, Error, Deleted
        public string Codec { get; set; } = "H264";
        public int Width { get; set; }
        public int Height { get; set; }
        public int Framerate { get; set; }
        public int BitrateKbps { get; set; }
        public string TriggerType { get; set; } = "Scheduled"; // Scheduled, Motion, Manual, Continuous
        public bool HasAudio { get; set; }

        public int ChunkCount { get; set; }
        public int ChunkDurationSeconds { get; set; } = 60; // Default 60s per chunk

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DeleteScheduledAt { get; set; }
        public bool IsDeleted { get; set; }
    }
}
