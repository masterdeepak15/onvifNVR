using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NVR.Core.DTOs
{
    // ===== AUTH =====
    public class LoginRequest
    {
        [Required] public string Username { get; set; } = string.Empty;
        [Required] public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        [Required] public string Username { get; set; } = string.Empty;
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Viewer";
    }

    public class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserDto User { get; set; } = null!;
    }

    public class RefreshTokenRequest
    {
        [Required] public string RefreshToken { get; set; } = string.Empty;
    }

    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime? LastLoginAt { get; set; }
    }

    // ===== CAMERA =====
    public class CameraDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public string RtspUrl { get; set; } = string.Empty;
        public string OnvifServiceUrl { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public bool IsRecording { get; set; }
        public bool PtzCapable { get; set; }
        public bool AudioEnabled { get; set; }
        public int Resolution_Width { get; set; }
        public int Resolution_Height { get; set; }
        public int Framerate { get; set; }
        public string Codec { get; set; } = string.Empty;
        public int GridPosition { get; set; }
        public Guid? StorageProfileId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public List<PtzPresetDto> PtzPresets { get; set; } = new();
    }

    public class AddCameraRequest
    {
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 80;
        [Required] public string Username { get; set; } = string.Empty;
        [Required] public string Password { get; set; } = string.Empty;
        public string? RtspUrl { get; set; }  // Manual override
        public Guid? StorageProfileId { get; set; }
        public bool AutoDiscover { get; set; } = true; // Auto-discover via ONVIF
    }

    public class UpdateCameraRequest
    {
        public string? Name { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? RtspUrl { get; set; }
        public Guid? StorageProfileId { get; set; }
        public bool? AudioEnabled { get; set; }
    }

    public class OnvifDiscoverResponse
    {
        public string XAddr { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public bool IsAlreadyAdded { get; set; }
    }

    // ===== PTZ =====
    public class PtzPresetDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string OnvifToken { get; set; } = string.Empty;
        public float? PanPosition { get; set; }
        public float? TiltPosition { get; set; }
        public float? ZoomPosition { get; set; }
        public string? ThumbnailPath { get; set; }
    }

    public class PtzMoveRequest
    {
        public float Pan { get; set; }
        public float Tilt { get; set; }
        public float Zoom { get; set; }
        public string MoveType { get; set; } = "Continuous"; // Absolute, Relative, Continuous
    }

    // ===== STORAGE =====
    public class StorageProfileDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool IsEnabled { get; set; }
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? Username { get; set; }
        public string? BasePath { get; set; }
        public string? ShareName { get; set; }
        public string? Region { get; set; }
        public string? ContainerName { get; set; }
        public long MaxStorageBytes { get; set; }
        public long UsedStorageBytes { get; set; }
        public int RetentionDays { get; set; }
        public bool AutoDeleteEnabled { get; set; }
        public bool IsHealthy { get; set; }
        public DateTime? LastHealthCheck { get; set; }
        public string? HealthError { get; set; }
        public double UsagePercent => MaxStorageBytes > 0 ? (double)UsedStorageBytes / MaxStorageBytes * 100 : 0;
    }

    public class CreateStorageProfileRequest
    {
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public string Type { get; set; } = string.Empty; // Local, NAS_SMB, NAS_NFS, S3, AzureBlob, FTP, SFTP
        public bool IsDefault { get; set; }
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? BasePath { get; set; }
        public string? ShareName { get; set; }
        public string? Region { get; set; }
        public string? AccessKey { get; set; }
        public string? SecretKey { get; set; }
        public string? ContainerName { get; set; }
        public string? ConnectionString { get; set; }
        public long MaxStorageBytes { get; set; } = 500L * 1024 * 1024 * 1024;
        public int RetentionDays { get; set; } = 30;
        public bool AutoDeleteEnabled { get; set; } = true;
    }

    // ===== RECORDING =====
    public class RecordingDto
    {
        public Guid Id { get; set; }
        public Guid CameraId { get; set; }
        public string CameraName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public long FileSizeBytes { get; set; }
        public int DurationSeconds { get; set; }
        public string Status { get; set; } = string.Empty;
        public string TriggerType { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public int ChunkCount { get; set; }
    }

    public class RecordingSearchDto
    {
        public Guid? CameraId { get; set; }
        public List<Guid>? CameraIds { get; set; }  // Multi-camera sync playback
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? TriggerType { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    // ===== SCHEDULE =====
    public class RecordingScheduleDto
    {
        public Guid Id { get; set; }
        public Guid CameraId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public int DaysOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string RecordingMode { get; set; } = string.Empty;
        public int ChunkDurationSeconds { get; set; }
        public int BitrateKbps { get; set; }
        public string Quality { get; set; } = string.Empty;
    }

    // ===== LAYOUT =====
    public class LayoutSaveRequest
    {
        [Required] public string LayoutName { get; set; } = "Default";
        [Required] public int GridColumns { get; set; } = 4;
        [Required] public List<CameraPositionRequest> Positions { get; set; } = new();
    }

    public class CameraPositionRequest
    {
        public Guid CameraId { get; set; }
        public int GridPosition { get; set; }
    }

    // ===== DASHBOARD =====
    public class DashboardSummaryDto
    {
        public int TotalCameras { get; set; }
        public int OnlineCameras { get; set; }
        public int RecordingCameras { get; set; }
        public int OfflineCameras { get; set; }
        public long TotalStorageBytes { get; set; }
        public long UsedStorageBytes { get; set; }
        public int ActiveAlerts { get; set; }
        public int TodayRecordingCount { get; set; }
        public List<StorageProfileDto> StorageSummaries { get; set; } = new();
    }

    // ===== PLAYBACK =====
    public class PlaybackRequest
    {
        [Required] public List<Guid> CameraIds { get; set; } = new();  // Multi-camera sync
        [Required] public DateTime Timestamp { get; set; }
        public float Speed { get; set; } = 1.0f;
    }

    public class PlaybackSessionDto
    {
        public string SessionId { get; set; } = string.Empty;
        public List<CameraPlaybackInfo> CameraStreams { get; set; } = new();
        public DateTime StartTimestamp { get; set; }
    }

    public class CameraPlaybackInfo
    {
        public Guid CameraId { get; set; }
        public string CameraName { get; set; } = string.Empty;
        public Guid? RecordingId { get; set; }
        public string? StreamUrl { get; set; }
        public bool HasRecording { get; set; }
        public List<TimelineSegmentDto> Timeline { get; set; } = new();
    }

    public class TimelineSegmentDto
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool HasMotion { get; set; }
        public string TriggerType { get; set; } = string.Empty;
    }
}
