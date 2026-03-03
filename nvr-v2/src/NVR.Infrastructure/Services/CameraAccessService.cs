using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NVR.Core.DTOs;
using NVR.Core.Entities;
using NVR.Core.Interfaces;
using NVR.Infrastructure.Data;

namespace NVR.Infrastructure.Services
{
    public class CameraAccessService : ICameraAccessService
    {
        private readonly NvrDbContext _db;

        public CameraAccessService(NvrDbContext db) => _db = db;

        public async Task<bool> HasPermissionAsync(string userId, Guid cameraId, string requiredPermission, CancellationToken ct = default)
        {
            // Admins have full access to everything
            var user = await _db.Users.FindAsync(new object[] { userId }, ct);
            if (user == null || !user.IsActive) return false;
            if (user.Role == "Admin") return true;

            // Operators get Control-level on all cameras by default
            if (user.Role == "Operator")
            {
                var operatorMin = CameraPermissions.Includes(CameraPermissions.Control, requiredPermission);
                if (operatorMin) return true;
                // Operators need explicit grant for Record/Admin
            }

            // Check explicit camera grant
            var access = await _db.CameraUserAccesses
                .FirstOrDefaultAsync(a =>
                    a.UserId == userId &&
                    a.CameraId == cameraId &&
                    a.IsActive &&
                    (a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow), ct);

            if (access == null) return false;
            return CameraPermissions.Includes(access.Permission, requiredPermission);
        }

        public async Task<List<CameraPermissionItem>> GetUserCameraPermissionsAsync(string userId, CancellationToken ct = default)
        {
            var user = await _db.Users.FindAsync(new object[] { userId }, ct);
            if (user == null) return new();

            var allCameras = await _db.Cameras.Select(c => new { c.Id, c.Name }).ToListAsync(ct);

            if (user.Role == "Admin")
            {
                // Admin gets Admin permission on all cameras
                return allCameras.Select(c => new CameraPermissionItem
                {
                    CameraId = c.Id,
                    CameraName = c.Name,
                    Permission = CameraPermissions.Admin,
                    IsExplicit = false
                }).ToList();
            }

            // Get explicit grants
            var grants = await _db.CameraUserAccesses
                .Where(a => a.UserId == userId && a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow))
                .ToDictionaryAsync(a => a.CameraId, ct);

            var result = new List<CameraPermissionItem>();

            foreach (var camera in allCameras)
            {
                if (user.Role == "Operator" && !grants.ContainsKey(camera.Id))
                {
                    // Operator default: Control access to all cameras
                    result.Add(new CameraPermissionItem
                    {
                        CameraId = camera.Id,
                        CameraName = camera.Name,
                        Permission = CameraPermissions.Control,
                        IsExplicit = false
                    });
                }
                else if (grants.TryGetValue(camera.Id, out var grant))
                {
                    result.Add(new CameraPermissionItem
                    {
                        CameraId = camera.Id,
                        CameraName = camera.Name,
                        Permission = grant.Permission,
                        IsExplicit = true
                    });
                }
                // Viewer with no explicit grant: no access
            }

            return result;
        }

        public async Task<List<CameraAccessDto>> GetCameraAccessListAsync(Guid cameraId, CancellationToken ct = default)
        {
            var accesses = await _db.CameraUserAccesses
                .Include(a => a.User)
                .Include(a => a.Camera)
                .Where(a => a.CameraId == cameraId && a.IsActive)
                .ToListAsync(ct);

            return accesses.Select(a => new CameraAccessDto
            {
                Id = a.Id,
                CameraId = a.CameraId,
                CameraName = a.Camera?.Name ?? string.Empty,
                UserId = a.UserId,
                Username = a.User?.Username ?? string.Empty,
                Permission = a.Permission,
                GrantedAt = a.GrantedAt,
                GrantedBy = a.GrantedBy,
                ExpiresAt = a.ExpiresAt,
                IsActive = a.IsActive
            }).ToList();
        }

        public async Task<CameraAccessDto> GrantAccessAsync(Guid cameraId, string grantedByUserId, GrantCameraAccessRequest request, CancellationToken ct = default)
        {
            // Upsert: update if already exists
            var existing = await _db.CameraUserAccesses
                .FirstOrDefaultAsync(a => a.CameraId == cameraId && a.UserId == request.UserId, ct);

            var grantedBy = await _db.Users.FindAsync(new object[] { grantedByUserId }, ct);

            if (existing != null)
            {
                existing.Permission = request.Permission;
                existing.ExpiresAt = request.ExpiresAt;
                existing.IsActive = true;
                existing.GrantedAt = DateTime.UtcNow;
                existing.GrantedBy = grantedBy?.Username ?? grantedByUserId;
            }
            else
            {
                existing = new CameraUserAccess
                {
                    CameraId = cameraId,
                    UserId = request.UserId,
                    Permission = request.Permission,
                    ExpiresAt = request.ExpiresAt,
                    GrantedBy = grantedBy?.Username ?? grantedByUserId
                };
                _db.CameraUserAccesses.Add(existing);
            }

            await _db.SaveChangesAsync(ct);

            var user = await _db.Users.FindAsync(new object[] { request.UserId }, ct);
            var camera = await _db.Cameras.FindAsync(new object[] { cameraId }, ct);

            return new CameraAccessDto
            {
                Id = existing.Id,
                CameraId = cameraId,
                CameraName = camera?.Name ?? string.Empty,
                UserId = request.UserId,
                Username = user?.Username ?? string.Empty,
                Permission = existing.Permission,
                GrantedAt = existing.GrantedAt,
                GrantedBy = existing.GrantedBy,
                ExpiresAt = existing.ExpiresAt,
                IsActive = true
            };
        }

        public async Task RevokeAccessAsync(Guid accessId, CancellationToken ct = default)
        {
            var access = await _db.CameraUserAccesses.FindAsync(new object[] { accessId }, ct);
            if (access == null) return;
            access.IsActive = false;
            await _db.SaveChangesAsync(ct);
        }

        public async Task<CameraAccessDto> UpdateAccessAsync(Guid accessId, string permission, CancellationToken ct = default)
        {
            var access = await _db.CameraUserAccesses
                .Include(a => a.User)
                .Include(a => a.Camera)
                .FirstOrDefaultAsync(a => a.Id == accessId, ct)
                ?? throw new KeyNotFoundException("Access record not found");

            access.Permission = permission;
            await _db.SaveChangesAsync(ct);

            return new CameraAccessDto
            {
                Id = access.Id,
                CameraId = access.CameraId,
                CameraName = access.Camera?.Name ?? string.Empty,
                UserId = access.UserId,
                Username = access.User?.Username ?? string.Empty,
                Permission = access.Permission,
                GrantedAt = access.GrantedAt
            };
        }

        public async Task<List<Guid>> FilterAccessibleCamerasAsync(string userId, IEnumerable<Guid> cameraIds, string requiredPermission = "View", CancellationToken ct = default)
        {
            var user = await _db.Users.FindAsync(new object[] { userId }, ct);
            if (user == null || !user.IsActive) return new();
            if (user.Role == "Admin") return cameraIds.ToList();

            var idList = cameraIds.ToList();
            var result = new List<Guid>();

            foreach (var camId in idList)
            {
                if (await HasPermissionAsync(userId, camId, requiredPermission, ct))
                    result.Add(camId);
            }
            return result;
        }
    }
}
