using System;
using System.Collections.Generic;

namespace NVR.Core.Entities
{
    public class StorageProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Local"; // Local, NAS_SMB, NAS_NFS, S3, AzureBlob, FTP, SFTP
        public bool IsDefault { get; set; }
        public bool IsEnabled { get; set; } = true;

        // Connection settings (stored encrypted)
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }       // Encrypted
        public string? BasePath { get; set; }       // Local path or remote path/bucket
        public string? ShareName { get; set; }      // SMB share name
        public string? Region { get; set; }         // S3 region
        public string? AccessKey { get; set; }      // S3/Azure access key
        public string? SecretKey { get; set; }      // S3/Azure secret (encrypted)
        public string? ContainerName { get; set; }  // Azure container or S3 bucket
        public string? ConnectionString { get; set; } // Azure connection string (encrypted)

        // Quota settings
        public long MaxStorageBytes { get; set; } = 500L * 1024 * 1024 * 1024; // 500GB default
        public long UsedStorageBytes { get; set; }
        public int RetentionDays { get; set; } = 30;    // Auto-delete after N days
        public bool AutoDeleteEnabled { get; set; } = true;
        public int LowSpaceWarningPercent { get; set; } = 85;

        public bool IsHealthy { get; set; } = true;
        public DateTime? LastHealthCheck { get; set; }
        public string? HealthError { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Relations
        public ICollection<Camera> Cameras { get; set; } = new List<Camera>();
    }
}
