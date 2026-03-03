using Microsoft.EntityFrameworkCore;
using NVR.Core.Entities;

namespace NVR.Infrastructure.Data
{
    public class NvrDbContext : DbContext
    {
        public NvrDbContext(DbContextOptions<NvrDbContext> options) : base(options) { }

        public DbSet<Camera> Cameras { get; set; } = null!;
        public DbSet<Recording> Recordings { get; set; } = null!;
        public DbSet<RecordingChunk> RecordingChunks { get; set; } = null!;
        public DbSet<StorageProfile> StorageProfiles { get; set; } = null!;
        public DbSet<RecordingSchedule> RecordingSchedules { get; set; } = null!;
        public DbSet<PtzPreset> PtzPresets { get; set; } = null!;
        public DbSet<CameraEvent> CameraEvents { get; set; } = null!;
        public DbSet<UserCameraLayout> UserCameraLayouts { get; set; } = null!;
        public DbSet<AppUser> Users { get; set; } = null!;
        public DbSet<SystemSetting> SystemSettings { get; set; } = null!;

        // NEW in v2
        public DbSet<CameraUserAccess> CameraUserAccesses { get; set; } = null!;
        public DbSet<StreamSession> StreamSessions { get; set; } = null!;
        public DbSet<CameraAnalyticsSnapshot> CameraAnalyticsSnapshots { get; set; } = null!;
        public DbSet<SystemAnalyticsSnapshot> SystemAnalyticsSnapshots { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // --- Camera ---
            modelBuilder.Entity<Camera>(e =>
            {
                e.HasKey(c => c.Id);
                e.HasIndex(c => c.IpAddress);
                e.HasIndex(c => c.Status);
                e.HasOne(c => c.StorageProfile)
                    .WithMany(s => s.Cameras)
                    .HasForeignKey(c => c.StorageProfileId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.Property(c => c.Name).HasMaxLength(100).IsRequired();
                e.Property(c => c.IpAddress).HasMaxLength(50).IsRequired();
                e.Property(c => c.Password).HasMaxLength(500);
            });

            // --- Recording ---
            modelBuilder.Entity<Recording>(e =>
            {
                e.HasKey(r => r.Id);
                e.HasIndex(r => r.CameraId);
                e.HasIndex(r => r.StartTime);
                e.HasIndex(r => new { r.CameraId, r.StartTime });
                e.HasIndex(r => r.Status);
                e.HasOne(r => r.Camera).WithMany(c => c.Recordings).HasForeignKey(r => r.CameraId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(r => r.StorageProfile).WithMany().HasForeignKey(r => r.StorageProfileId).OnDelete(DeleteBehavior.Restrict);
            });

            // --- RecordingChunk ---
            modelBuilder.Entity<RecordingChunk>(e =>
            {
                e.HasKey(c => c.Id);
                e.HasIndex(c => c.RecordingId);
                e.HasIndex(c => c.StartTime);
                e.HasIndex(c => new { c.CameraId, c.StartTime });
                e.HasOne(c => c.Recording).WithMany().HasForeignKey(c => c.RecordingId).OnDelete(DeleteBehavior.Cascade);
            });

            // --- StorageProfile ---
            modelBuilder.Entity<StorageProfile>(e =>
            {
                e.HasKey(s => s.Id);
                e.Property(s => s.Name).HasMaxLength(100).IsRequired();
                e.Property(s => s.Password).HasMaxLength(1000);
                e.Property(s => s.SecretKey).HasMaxLength(1000);
                e.Property(s => s.ConnectionString).HasMaxLength(2000);
            });

            // --- RecordingSchedule ---
            modelBuilder.Entity<RecordingSchedule>(e =>
            {
                e.HasKey(s => s.Id);
                e.HasOne(s => s.Camera).WithMany(c => c.RecordingSchedules).HasForeignKey(s => s.CameraId).OnDelete(DeleteBehavior.Cascade);
            });

            // --- PtzPreset ---
            modelBuilder.Entity<PtzPreset>(e =>
            {
                e.HasKey(p => p.Id);
                e.HasOne(p => p.Camera).WithMany(c => c.PtzPresets).HasForeignKey(p => p.CameraId).OnDelete(DeleteBehavior.Cascade);
                e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            });

            // --- CameraEvent ---
            modelBuilder.Entity<CameraEvent>(e =>
            {
                e.HasKey(ev => ev.Id);
                e.HasIndex(ev => ev.CameraId);
                e.HasIndex(ev => ev.Timestamp);
                e.HasIndex(ev => new { ev.CameraId, ev.Timestamp });
                e.HasIndex(ev => ev.IsAcknowledged);
                e.HasOne(ev => ev.Camera).WithMany(c => c.Events).HasForeignKey(ev => ev.CameraId).OnDelete(DeleteBehavior.Cascade);
            });

            // --- UserCameraLayout ---
            modelBuilder.Entity<UserCameraLayout>(e =>
            {
                e.HasKey(l => l.Id);
                e.HasIndex(l => new { l.UserId, l.LayoutName, l.GridPosition }).IsUnique();
                e.HasOne(l => l.Camera).WithMany(c => c.UserLayouts).HasForeignKey(l => l.CameraId).OnDelete(DeleteBehavior.Cascade);
            });

            // --- AppUser ---
            modelBuilder.Entity<AppUser>(e =>
            {
                e.HasKey(u => u.Id);
                e.HasIndex(u => u.Username).IsUnique();
                e.HasIndex(u => u.Email).IsUnique();
                e.Property(u => u.Username).HasMaxLength(50).IsRequired();
                e.Property(u => u.Email).HasMaxLength(200).IsRequired();
                e.HasMany(u => u.CameraLayouts).WithOne().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            // --- SystemSetting ---
            modelBuilder.Entity<SystemSetting>(e =>
            {
                e.HasKey(s => s.Id);
                e.HasIndex(s => s.Key).IsUnique();
            });

            // ============================================================
            // NEW v2 ENTITIES
            // ============================================================

            // --- CameraUserAccess ---
            modelBuilder.Entity<CameraUserAccess>(e =>
            {
                e.HasKey(a => a.Id);
                e.HasIndex(a => new { a.CameraId, a.UserId });
                e.HasIndex(a => a.UserId);
                e.HasOne(a => a.Camera).WithMany().HasForeignKey(a => a.CameraId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
                e.Property(a => a.Permission).HasMaxLength(20).IsRequired();
                e.Property(a => a.GrantedBy).HasMaxLength(100);
            });

            // --- StreamSession ---
            modelBuilder.Entity<StreamSession>(e =>
            {
                e.HasKey(s => s.Id);
                e.HasIndex(s => s.ConnectionId);
                e.HasIndex(s => s.CameraId);
                e.HasIndex(s => new { s.IsActive, s.SessionType });
                e.HasIndex(s => s.UserId);
                e.Property(s => s.ConnectionId).HasMaxLength(200);
                e.Property(s => s.ClientIp).HasMaxLength(50);
            });

            // --- CameraAnalyticsSnapshot ---
            modelBuilder.Entity<CameraAnalyticsSnapshot>(e =>
            {
                e.HasKey(s => s.Id);
                e.HasIndex(s => new { s.CameraId, s.Hour }).IsUnique();
                e.HasOne(s => s.Camera).WithMany().HasForeignKey(s => s.CameraId).OnDelete(DeleteBehavior.Cascade);
            });

            // --- SystemAnalyticsSnapshot ---
            modelBuilder.Entity<SystemAnalyticsSnapshot>(e =>
            {
                e.HasKey(s => s.Id);
                e.HasIndex(s => s.Date).IsUnique();
            });
        }
    }
}
