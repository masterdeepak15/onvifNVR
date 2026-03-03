using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NVR.Core.Entities;

namespace NVR.Core.Interfaces
{
    // ============================================================
    // STORAGE PROVIDER INTERFACE - pluggable storage backends
    // ============================================================
    public interface IStorageProvider
    {
        string ProviderType { get; } // Local, NAS_SMB, S3, AzureBlob, etc.

        Task<bool> TestConnectionAsync(StorageProfile profile, CancellationToken ct = default);
        Task<Stream> OpenReadAsync(StorageProfile profile, string relativePath, CancellationToken ct = default);
        Task WriteAsync(StorageProfile profile, string relativePath, Stream data, CancellationToken ct = default);
        Task DeleteAsync(StorageProfile profile, string relativePath, CancellationToken ct = default);
        Task<bool> ExistsAsync(StorageProfile profile, string relativePath, CancellationToken ct = default);
        Task<long> GetUsedSpaceAsync(StorageProfile profile, CancellationToken ct = default);
        Task<IEnumerable<string>> ListFilesAsync(StorageProfile profile, string directory, CancellationToken ct = default);
        Task<StorageFileInfo> GetFileInfoAsync(StorageProfile profile, string relativePath, CancellationToken ct = default);
        Task CreateDirectoryAsync(StorageProfile profile, string relativePath, CancellationToken ct = default);
    }

    public record StorageFileInfo(string Path, long SizeBytes, DateTime LastModified, bool Exists);

    // ============================================================
    // STORAGE PROVIDER FACTORY
    // ============================================================
    public interface IStorageProviderFactory
    {
        IStorageProvider GetProvider(string providerType);
        IStorageProvider GetProvider(StorageProfile profile);
        IEnumerable<string> GetAvailableProviderTypes();
    }

    // ============================================================
    // ONVIF SERVICE INTERFACE
    // ============================================================
    public interface IOnvifService
    {
        Task<IEnumerable<OnvifDiscoveredDevice>> DiscoverDevicesAsync(int timeoutMs = 5000, CancellationToken ct = default);
        Task<OnvifDeviceInfo> GetDeviceInfoAsync(Camera camera, CancellationToken ct = default);
        Task<IEnumerable<OnvifProfile>> GetProfilesAsync(Camera camera, CancellationToken ct = default);
        Task<string> GetRtspStreamUriAsync(Camera camera, string profileToken, CancellationToken ct = default);
        Task<OnvifPtzStatus> GetPtzStatusAsync(Camera camera, CancellationToken ct = default);
        Task PtzAbsoluteMoveAsync(Camera camera, float pan, float tilt, float zoom, CancellationToken ct = default);
        Task PtzRelativeMoveAsync(Camera camera, float panDelta, float tiltDelta, float zoomDelta, CancellationToken ct = default);
        Task PtzContinuousMoveAsync(Camera camera, float panSpeed, float tiltSpeed, float zoomSpeed, CancellationToken ct = default);
        Task PtzStopAsync(Camera camera, CancellationToken ct = default);
        Task<IEnumerable<OnvifPtzPreset>> GetPresetsAsync(Camera camera, CancellationToken ct = default);
        Task GoToPresetAsync(Camera camera, string presetToken, CancellationToken ct = default);
        Task<string> SetPresetAsync(Camera camera, string presetName, CancellationToken ct = default);
        Task RemovePresetAsync(Camera camera, string presetToken, CancellationToken ct = default);
        Task<byte[]> GetSnapshotAsync(Camera camera, CancellationToken ct = default);
        Task<OnvifVideoConfig> GetVideoConfigAsync(Camera camera, string profileToken, CancellationToken ct = default);
        Task<bool> PingAsync(string ipAddress, int port = 80, CancellationToken ct = default);


        // ---- Imaging Service (Focus + Iris) ----
        /// <summary>Move focus in a continuous direction. Speed: -1.0 (near) to 1.0 (far). Stop with speed = 0.</summary>
        Task FocusContinuousMoveAsync(Camera camera, float speed, CancellationToken ct = default);
        /// <summary>Move focus to absolute position. Position: 0.0 (near limit) to 1.0 (far limit).</summary>
        Task FocusAbsoluteMoveAsync(Camera camera, float position, float speed = 1.0f, CancellationToken ct = default);
        /// <summary>Enable or disable auto-focus.</summary>
        Task SetFocusModeAsync(Camera camera, FocusMode mode, CancellationToken ct = default);
        /// <summary>Get current focus settings (mode + position).</summary>
        Task<OnvifFocusStatus> GetFocusStatusAsync(Camera camera, CancellationToken ct = default);
        /// <summary>Set iris to MANUAL mode and move to target level. Level: 0.0 (closed) to 1.0 (fully open).</summary>
        Task SetIrisAsync(Camera camera, float level, CancellationToken ct = default);
        /// <summary>Enable or disable auto-iris (exposure mode).</summary>
        Task SetIrisModeAsync(Camera camera, IrisMode mode, CancellationToken ct = default);
        /// <summary>Get current iris / exposure settings.</summary>
        Task<OnvifIrisStatus> GetIrisStatusAsync(Camera camera, CancellationToken ct = default);
    }

    public enum FocusMode { Auto, Manual }
    public enum IrisMode { Auto, Manual }

    public record OnvifDiscoveredDevice(string Xaddr, string IpAddress, int Port, string Types, string Scopes);
    public record OnvifDeviceInfo(string Manufacturer, string Model, string FirmwareVersion, string SerialNumber, string HardwareId);
    public record OnvifProfile(string Token, string Name, string VideoEncoding, int Width, int Height, int Framerate);
    public record OnvifPtzStatus(float Pan, float Tilt, float Zoom, string MoveStatus);
    public record OnvifPtzPreset(string Token, string Name, float? Pan, float? Tilt, float? Zoom);
    public record OnvifVideoConfig(int Width, int Height, int Framerate, int BitrateKbps, string Encoding);
    public record OnvifFocusStatus(FocusMode Mode, float Position, string MoveStatus);
    public record OnvifIrisStatus(IrisMode Mode, float Level);
    // ============================================================
    // RTSP / STREAM SERVICE
    // ============================================================
    public interface IRtspStreamService
    {
        Task StartStreamAsync(Guid cameraId, CancellationToken ct = default);
        Task StopStreamAsync(Guid cameraId);
        Task<bool> IsStreamingAsync(Guid cameraId);
        IAsyncEnumerable<byte[]> GetFrameStreamAsync(Guid cameraId, CancellationToken ct = default);
        Task<byte[]> GetLatestFrameAsync(Guid cameraId, CancellationToken ct = default);
    }

    // ============================================================
    // RECORDING SERVICE
    // ============================================================
    public interface IRecordingService
    {
        Task StartRecordingAsync(Guid cameraId, CancellationToken ct = default);
        Task StopRecordingAsync(Guid cameraId);
        Task<bool> IsRecordingAsync(Guid cameraId);
        Task<Recording?> GetActiveRecordingAsync(Guid cameraId);
        Task FinalizeChunkAsync(Guid recordingId, CancellationToken ct = default);
    }

    // ============================================================
    // CAMERA REPOSITORY
    // ============================================================
    public interface ICameraRepository
    {
        Task<IEnumerable<Camera>> GetAllAsync();
        Task<Camera?> GetByIdAsync(Guid id);
        Task<Camera> CreateAsync(Camera camera);
        Task<Camera> UpdateAsync(Camera camera);
        Task DeleteAsync(Guid id);
        Task<bool> ExistsAsync(Guid id);
    }

    // ============================================================
    // RECORDING REPOSITORY
    // ============================================================
    public interface IRecordingRepository
    {
        Task<IEnumerable<Recording>> SearchAsync(RecordingSearchRequest request);
        Task<Recording?> GetByIdAsync(Guid id);
        Task<Recording> CreateAsync(Recording recording);
        Task<Recording> UpdateAsync(Recording recording);
        Task<IEnumerable<RecordingChunk>> GetChunksAsync(Guid recordingId);
        Task<RecordingChunk> AddChunkAsync(RecordingChunk chunk);
        Task<IEnumerable<Recording>> GetRecordingsForDeletionAsync(int olderThanDays, Guid? storageProfileId = null);
        Task<long> GetTotalSizeByCameraAsync(Guid cameraId);
    }

    public class RecordingSearchRequest
    {
        public Guid? CameraId { get; set; }
        public IEnumerable<Guid>? CameraIds { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? TriggerType { get; set; }
        public bool? HasMotion { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
