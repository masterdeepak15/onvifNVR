using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NVR.Core.Interfaces
{
    public interface INvrHubEventEmitter
    {
        Task SendCameraStatusAsync(Guid cameraId, string status, bool isOnline, bool isRecording, int viewers = 0);

        Task SendAlertAsync(Guid? cameraId, string cameraName, string alertType, string message, string severity = "Warning", string? snapshotBase64 = null);

        Task SendRecordingStatusAsync(Guid cameraId, Guid? recordingId, string status, int? chunk = null, long? sizeBytes = null);

        Task SendStorageAlertAsync(Guid storageProfileId, double usagePercent, string message);

        Task SendAnalyticsUpdateAsync(int online, int recording, int viewers, double storagePercent, int unackedAlerts);

        Task SendUserAlertAsync(string userId, string message, string severity = "Info");
    }
}
