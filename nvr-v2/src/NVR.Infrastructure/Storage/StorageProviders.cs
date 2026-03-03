using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NVR.Core.Entities;
using NVR.Core.Interfaces;

namespace NVR.Infrastructure.Storage
{
    // ============================================================
    // LOCAL FILE SYSTEM PROVIDER
    // ============================================================
    public class LocalStorageProvider : IStorageProvider
    {
        public string ProviderType => "Local";
        private readonly ILogger<LocalStorageProvider> _logger;

        public LocalStorageProvider(ILogger<LocalStorageProvider> logger) => _logger = logger;

        private string GetFullPath(StorageProfile profile, string relativePath) =>
            Path.Combine(profile.BasePath ?? "/nvr/recordings", relativePath.TrimStart('/'));

        public Task<bool> TestConnectionAsync(StorageProfile profile, CancellationToken ct = default)
        {
            try
            {
                var basePath = profile.BasePath ?? "/nvr/recordings";
                Directory.CreateDirectory(basePath);
                var testFile = Path.Combine(basePath, ".nvr_test");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Local storage test failed");
                return Task.FromResult(false);
            }
        }

        public async Task<Stream> OpenReadAsync(StorageProfile profile, string relativePath, CancellationToken ct = default)
        {
            var fullPath = GetFullPath(profile, relativePath);
            return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        }

        public async Task WriteAsync(StorageProfile profile, string relativePath, Stream data, CancellationToken ct = default)
        {
            var fullPath = GetFullPath(profile, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);
            await data.CopyToAsync(fs, ct);
        }

        public Task DeleteAsync(StorageProfile profile, string relativePath, CancellationToken ct = default)
        {
            var fullPath = GetFullPath(profile, relativePath);
            if (File.Exists(fullPath)) File.Delete(fullPath);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(StorageProfile profile, string relativePath, CancellationToken ct = default)
            => Task.FromResult(File.Exists(GetFullPath(profile, relativePath)));

        public Task<long> GetUsedSpaceAsync(StorageProfile profile, CancellationToken ct = default)
        {
            var basePath = profile.BasePath ?? "/nvr/recordings";
            if (!Directory.Exists(basePath)) return Task.FromResult(0L);
            var size = Directory.EnumerateFiles(basePath, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);
            return Task.FromResult(size);
        }

        public Task<IEnumerable<string>> ListFilesAsync(StorageProfile profile, string directory, CancellationToken ct = default)
        {
            var fullPath = GetFullPath(profile, directory);
            if (!Directory.Exists(fullPath)) return Task.FromResult(Enumerable.Empty<string>());
            var files = Directory.EnumerateFiles(fullPath).Select(f => Path.GetRelativePath(profile.BasePath!, f));
            return Task.FromResult(files);
        }

        public Task<StorageFileInfo> GetFileInfoAsync(StorageProfile profile, string relativePath, CancellationToken ct = default)
        {
            var fullPath = GetFullPath(profile, relativePath);
            var fi = new FileInfo(fullPath);
            return Task.FromResult(new StorageFileInfo(relativePath, fi.Exists ? fi.Length : 0, fi.Exists ? fi.LastWriteTimeUtc : DateTime.MinValue, fi.Exists));
        }

        public Task CreateDirectoryAsync(StorageProfile profile, string relativePath, CancellationToken ct = default)
        {
            Directory.CreateDirectory(GetFullPath(profile, relativePath));
            return Task.CompletedTask;
        }
    }

    // ============================================================
    // SMB/NAS PROVIDER (uses mounted paths on Linux/Windows)
    // ============================================================
    public class NasSmbStorageProvider : LocalStorageProvider
    {
        public new string ProviderType => "NAS_SMB";
        private readonly ILogger<NasSmbStorageProvider> _log;

        public NasSmbStorageProvider(ILogger<NasSmbStorageProvider> log) : base(log) => _log = log;

        public new async Task<bool> TestConnectionAsync(StorageProfile profile, CancellationToken ct = default)
        {
            // NAS SMB: basePath should be the mounted path e.g. /mnt/nas/nvr
            // On Linux: mount -t cifs //host/share /mnt/nas -o username=x,password=y
            return await base.TestConnectionAsync(profile, ct);
        }
    }

    // ============================================================
    // S3 PROVIDER
    // ============================================================
    public class S3StorageProvider : IStorageProvider
    {
        public string ProviderType => "S3";
        private readonly ILogger<S3StorageProvider> _logger;

        public S3StorageProvider(ILogger<S3StorageProvider> logger) => _logger = logger;

        // NOTE: In production add AWSSDK.S3 NuGet package and implement using AmazonS3Client
        // This is a stub showing the interface contract
        private Amazon.S3.AmazonS3Client? GetClient(StorageProfile profile)
        {
            var config = new Amazon.S3.AmazonS3Config { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(profile.Region ?? "us-east-1") };
            return new Amazon.S3.AmazonS3Client(profile.AccessKey, profile.SecretKey, config);
        }

        public async Task<bool> TestConnectionAsync(StorageProfile profile, CancellationToken ct = default)
        {
            try
            {
                using var client = GetClient(profile);
                var response = await client!.ListBucketsAsync(ct);
                return response.HttpStatusCode == HttpStatusCode.OK;
            }
            catch (Exception ex) { _logger.LogError(ex, "S3 test failed"); return false; }
        }

        public async Task<Stream> OpenReadAsync(StorageProfile profile, string relativePath, CancellationToken ct = default)
        {
            using var client = GetClient(profile);
            var response = await client!.GetObjectAsync(profile.ContainerName!, relativePath, ct);
            return response.ResponseStream;
        }

        public async Task WriteAsync(StorageProfile profile, string relativePath, Stream data, CancellationToken ct = default)
        {
            using var client = GetClient(profile);
            var request = new Amazon.S3.Model.PutObjectRequest
            {
                BucketName = profile.ContainerName,
                Key = relativePath,
                InputStream = data
            };
            await client!.PutObjectAsync(request, ct);
        }

        public async Task DeleteAsync(StorageProfile profile, string relativePath, CancellationToken ct = default)
        {
            using var client = GetClient(profile);
            await client!.DeleteObjectAsync(profile.ContainerName!, relativePath, ct);
        }

        public async Task<bool> ExistsAsync(StorageProfile profile, string relativePath, CancellationToken ct = default)
        {
            try
            {
                using var client = GetClient(profile);
                await client!.GetObjectMetadataAsync(profile.ContainerName!, relativePath, ct);
                return true;
            }
            catch { return false; }
        }

        public async Task<long> GetUsedSpaceAsync(StorageProfile profile, CancellationToken ct = default)
        {
            // Use CloudWatch metrics or list objects - simplified stub
            return 0;
        }

        public async Task<IEnumerable<string>> ListFilesAsync(StorageProfile profile, string directory, CancellationToken ct = default)
        {
            using var client = GetClient(profile);
            var request = new Amazon.S3.Model.ListObjectsV2Request { BucketName = profile.ContainerName!, Prefix = directory };
            var response = await client!.ListObjectsV2Async(request, ct);
            return response.S3Objects.Select(o => o.Key);
        }

        public async Task<StorageFileInfo> GetFileInfoAsync(StorageProfile profile, string relativePath, CancellationToken ct = default)
        {
            try
            {
                using var client = GetClient(profile);
                var meta = await client!.GetObjectMetadataAsync(profile.ContainerName!, relativePath, ct);
                return new StorageFileInfo(relativePath, meta.ContentLength, meta.LastModified, true);
            }
            catch { return new StorageFileInfo(relativePath, 0, DateTime.MinValue, false); }
        }

        public Task CreateDirectoryAsync(StorageProfile profile, string relativePath, CancellationToken ct = default)
            => Task.CompletedTask; // S3 has no directories
    }

    // ============================================================
    // AZURE BLOB PROVIDER
    // ============================================================
    public class AzureBlobStorageProvider : IStorageProvider
    {
        public string ProviderType => "AzureBlob";
        private readonly ILogger<AzureBlobStorageProvider> _logger;

        public AzureBlobStorageProvider(ILogger<AzureBlobStorageProvider> logger) => _logger = logger;

        // NOTE: Add Azure.Storage.Blobs NuGet package in production
        private Azure.Storage.Blobs.BlobContainerClient GetContainer(StorageProfile profile)
        {
            var serviceClient = new Azure.Storage.Blobs.BlobServiceClient(profile.ConnectionString);
            return serviceClient.GetBlobContainerClient(profile.ContainerName);
        }

        public async Task<bool> TestConnectionAsync(StorageProfile profile, CancellationToken ct = default)
        {
            try
            {
                var container = GetContainer(profile);
                await container.CreateIfNotExistsAsync(cancellationToken: ct);
                return true;
            }
            catch (Exception ex) { _logger.LogError(ex, "Azure test failed"); return false; }
        }

        public async Task<Stream> OpenReadAsync(StorageProfile profile, string relativePath, CancellationToken ct = default)
        {
            var blob = GetContainer(profile).GetBlobClient(relativePath);
            var response = await blob.DownloadStreamingAsync(cancellationToken: ct);
            return response.Value.Content;
        }

        public async Task WriteAsync(StorageProfile profile, string relativePath, Stream data, CancellationToken ct = default)
        {
            var blob = GetContainer(profile).GetBlobClient(relativePath);
            await blob.UploadAsync(data, overwrite: true, cancellationToken: ct);
        }

        public async Task DeleteAsync(StorageProfile profile, string relativePath, CancellationToken ct = default)
        {
            var blob = GetContainer(profile).GetBlobClient(relativePath);
            await blob.DeleteIfExistsAsync(cancellationToken: ct);
        }

        public async Task<bool> ExistsAsync(StorageProfile profile, string relativePath, CancellationToken ct = default)
        {
            var blob = GetContainer(profile).GetBlobClient(relativePath);
            return await blob.ExistsAsync(ct);
        }

        public async Task<long> GetUsedSpaceAsync(StorageProfile profile, CancellationToken ct = default)
        {
            long total = 0;
            await foreach (var blob in GetContainer(profile).GetBlobsAsync(cancellationToken: ct))
                total += blob.Properties.ContentLength ?? 0;
            return total;
        }

        public async Task<IEnumerable<string>> ListFilesAsync(StorageProfile profile, string directory, CancellationToken ct = default)
        {
            var files = new List<string>();
            await foreach (var blob in GetContainer(profile).GetBlobsAsync(prefix: directory, cancellationToken: ct))
                files.Add(blob.Name);
            return files;
        }

        public async Task<StorageFileInfo> GetFileInfoAsync(StorageProfile profile, string relativePath, CancellationToken ct = default)
        {
            var blob = GetContainer(profile).GetBlobClient(relativePath);
            if (!await blob.ExistsAsync(ct)) return new StorageFileInfo(relativePath, 0, DateTime.MinValue, false);
            var props = await blob.GetPropertiesAsync(cancellationToken: ct);
            return new StorageFileInfo(relativePath, props.Value.ContentLength, props.Value.LastModified.UtcDateTime, true);
        }

        public Task CreateDirectoryAsync(StorageProfile profile, string relativePath, CancellationToken ct = default)
            => Task.CompletedTask; // Azure Blob has no directories
    }

    // ============================================================
    // FACTORY
    // ============================================================
    public class StorageProviderFactory : IStorageProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public StorageProviderFactory(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

        public IStorageProvider GetProvider(string providerType) => providerType switch
        {
            "Local" => (IStorageProvider)_serviceProvider.GetService(typeof(LocalStorageProvider))!,
            "NAS_SMB" or "NAS_NFS" => (IStorageProvider)_serviceProvider.GetService(typeof(NasSmbStorageProvider))!,
            "S3" => (IStorageProvider)_serviceProvider.GetService(typeof(S3StorageProvider))!,
            "AzureBlob" => (IStorageProvider)_serviceProvider.GetService(typeof(AzureBlobStorageProvider))!,
            _ => throw new NotSupportedException($"Storage type '{providerType}' is not supported")
        };

        public IStorageProvider GetProvider(StorageProfile profile) => GetProvider(profile.Type);

        public IEnumerable<string> GetAvailableProviderTypes() =>
            new[] { "Local", "NAS_SMB", "NAS_NFS", "S3", "AzureBlob", "FTP", "SFTP" };
    }
}
