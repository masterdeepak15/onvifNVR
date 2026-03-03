using System;

namespace NVR.Core.Entities
{
    /// <summary>
    /// NVR Chunk Protocol (NCP) - Individual recording segment
    /// Chunks are small MPEG-TS files for efficient seeking, streaming and storage
    /// Naming: {cameraId}/{yyyy-MM-dd}/{HH}/{timestamp}_{seq:D6}.ncp
    /// </summary>
    public class RecordingChunk
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RecordingId { get; set; }
        public Recording? Recording { get; set; }
        public Guid CameraId { get; set; }

        public int SequenceNumber { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int DurationMs { get; set; }

        public string FilePath { get; set; } = string.Empty;   // Relative path in storage provider
        public long FileSizeBytes { get; set; }

        // Keyframe index for fast seeking (JSON: [{pts_ms: 0, offset: 0}, ...])
        public string? KeyframeIndex { get; set; }

        public bool HasMotion { get; set; }
        public float? MotionScore { get; set; }  // 0-1 motion intensity

        public string Status { get; set; } = "Pending"; // Pending, Written, Verified, Error
        public string? Checksum { get; set; }  // SHA256 for integrity

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
