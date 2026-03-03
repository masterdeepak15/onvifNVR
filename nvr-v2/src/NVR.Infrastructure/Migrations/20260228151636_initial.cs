using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NVR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StorageProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Host = table.Column<string>(type: "TEXT", nullable: true),
                    Port = table.Column<int>(type: "INTEGER", nullable: true),
                    Username = table.Column<string>(type: "TEXT", nullable: true),
                    Password = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    BasePath = table.Column<string>(type: "TEXT", nullable: true),
                    ShareName = table.Column<string>(type: "TEXT", nullable: true),
                    Region = table.Column<string>(type: "TEXT", nullable: true),
                    AccessKey = table.Column<string>(type: "TEXT", nullable: true),
                    SecretKey = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ContainerName = table.Column<string>(type: "TEXT", nullable: true),
                    ConnectionString = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    MaxStorageBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    UsedStorageBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    RetentionDays = table.Column<int>(type: "INTEGER", nullable: false),
                    AutoDeleteEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LowSpaceWarningPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    IsHealthy = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastHealthCheck = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HealthError = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StreamSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CameraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ConnectionId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ClientIp = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BytesStreamed = table.Column<long>(type: "INTEGER", nullable: false),
                    FramesSent = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionType = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemAnalyticsSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalCameras = table.Column<int>(type: "INTEGER", nullable: false),
                    OnlineCameras = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordingCameras = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalRecordingSeconds = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalStorageBytesWritten = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalStorageBytesDeleted = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalMotionEvents = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalViewerSessions = table.Column<int>(type: "INTEGER", nullable: false),
                    PeakConcurrentViewers = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalAlerts = table.Column<int>(type: "INTEGER", nullable: false),
                    AcknowledgedAlerts = table.Column<int>(type: "INTEGER", nullable: false),
                    StorageUsagePercent = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAnalyticsSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RefreshToken = table.Column<string>(type: "TEXT", nullable: true),
                    RefreshTokenExpiry = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cameras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    Password = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RtspUrl = table.Column<string>(type: "TEXT", nullable: false),
                    OnvifServiceUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Manufacturer = table.Column<string>(type: "TEXT", nullable: false),
                    Model = table.Column<string>(type: "TEXT", nullable: false),
                    FirmwareVersion = table.Column<string>(type: "TEXT", nullable: false),
                    SerialNumber = table.Column<string>(type: "TEXT", nullable: false),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRecording = table.Column<bool>(type: "INTEGER", nullable: false),
                    PtzCapable = table.Column<bool>(type: "INTEGER", nullable: false),
                    AudioEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Resolution_Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Resolution_Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Framerate = table.Column<int>(type: "INTEGER", nullable: false),
                    Codec = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GridPosition = table.Column<int>(type: "INTEGER", nullable: false),
                    StorageProfileId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cameras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cameras_StorageProfiles_StorageProfileId",
                        column: x => x.StorageProfileId,
                        principalTable: "StorageProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CameraAnalyticsSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CameraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Hour = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UptimeSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordingSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    StorageBytesWritten = table.Column<long>(type: "INTEGER", nullable: false),
                    MotionEventCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ViewerCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AvgBitrateKbps = table.Column<float>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraAnalyticsSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CameraAnalyticsSnapshots_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CameraEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CameraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    SnapshotPath = table.Column<string>(type: "TEXT", nullable: true),
                    RecordingId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsAcknowledged = table.Column<bool>(type: "INTEGER", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CameraEvents_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CameraUserAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CameraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Permission = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GrantedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraUserAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CameraUserAccesses_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CameraUserAccesses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PtzPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CameraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    OnvifToken = table.Column<string>(type: "TEXT", nullable: false),
                    PanPosition = table.Column<float>(type: "REAL", nullable: true),
                    TiltPosition = table.Column<float>(type: "REAL", nullable: true),
                    ZoomPosition = table.Column<float>(type: "REAL", nullable: true),
                    ThumbnailPath = table.Column<string>(type: "TEXT", nullable: true),
                    OrderIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PtzPresets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PtzPresets_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recordings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CameraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StorageProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    StoragePath = table.Column<string>(type: "TEXT", nullable: false),
                    IndexPath = table.Column<string>(type: "TEXT", nullable: false),
                    ThumbnailPath = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Codec = table.Column<string>(type: "TEXT", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Framerate = table.Column<int>(type: "INTEGER", nullable: false),
                    BitrateKbps = table.Column<int>(type: "INTEGER", nullable: false),
                    TriggerType = table.Column<string>(type: "TEXT", nullable: false),
                    HasAudio = table.Column<bool>(type: "INTEGER", nullable: false),
                    ChunkCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ChunkDurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeleteScheduledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recordings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recordings_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Recordings_StorageProfiles_StorageProfileId",
                        column: x => x.StorageProfileId,
                        principalTable: "StorageProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecordingSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CameraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DaysOfWeek = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    Continuous = table.Column<bool>(type: "INTEGER", nullable: false),
                    RecordingMode = table.Column<string>(type: "TEXT", nullable: false),
                    ChunkDurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    BitrateKbps = table.Column<int>(type: "INTEGER", nullable: false),
                    Quality = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordingSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecordingSchedules_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCameraLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    CameraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GridPosition = table.Column<int>(type: "INTEGER", nullable: false),
                    GridColumns = table.Column<int>(type: "INTEGER", nullable: false),
                    LayoutName = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCameraLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCameraLayouts_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCameraLayouts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecordingChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecordingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CameraId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SequenceNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    KeyframeIndex = table.Column<string>(type: "TEXT", nullable: true),
                    HasMotion = table.Column<bool>(type: "INTEGER", nullable: false),
                    MotionScore = table.Column<float>(type: "REAL", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Checksum = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordingChunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecordingChunks_Recordings_RecordingId",
                        column: x => x.RecordingId,
                        principalTable: "Recordings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CameraAnalyticsSnapshots_CameraId_Hour",
                table: "CameraAnalyticsSnapshots",
                columns: new[] { "CameraId", "Hour" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CameraEvents_CameraId",
                table: "CameraEvents",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_CameraEvents_CameraId_Timestamp",
                table: "CameraEvents",
                columns: new[] { "CameraId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_CameraEvents_IsAcknowledged",
                table: "CameraEvents",
                column: "IsAcknowledged");

            migrationBuilder.CreateIndex(
                name: "IX_CameraEvents_Timestamp",
                table: "CameraEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_IpAddress",
                table: "Cameras",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_Status",
                table: "Cameras",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_StorageProfileId",
                table: "Cameras",
                column: "StorageProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CameraUserAccesses_CameraId_UserId",
                table: "CameraUserAccesses",
                columns: new[] { "CameraId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CameraUserAccesses_UserId",
                table: "CameraUserAccesses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PtzPresets_CameraId",
                table: "PtzPresets",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_RecordingChunks_CameraId_StartTime",
                table: "RecordingChunks",
                columns: new[] { "CameraId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordingChunks_RecordingId",
                table: "RecordingChunks",
                column: "RecordingId");

            migrationBuilder.CreateIndex(
                name: "IX_RecordingChunks_StartTime",
                table: "RecordingChunks",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_Recordings_CameraId",
                table: "Recordings",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_Recordings_CameraId_StartTime",
                table: "Recordings",
                columns: new[] { "CameraId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Recordings_StartTime",
                table: "Recordings",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_Recordings_Status",
                table: "Recordings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Recordings_StorageProfileId",
                table: "Recordings",
                column: "StorageProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RecordingSchedules_CameraId",
                table: "RecordingSchedules",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_CameraId",
                table: "StreamSessions",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_ConnectionId",
                table: "StreamSessions",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_IsActive_SessionType",
                table: "StreamSessions",
                columns: new[] { "IsActive", "SessionType" });

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_UserId",
                table: "StreamSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAnalyticsSnapshots_Date",
                table: "SystemAnalyticsSnapshots",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCameraLayouts_CameraId",
                table: "UserCameraLayouts",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCameraLayouts_UserId_LayoutName_GridPosition",
                table: "UserCameraLayouts",
                columns: new[] { "UserId", "LayoutName", "GridPosition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CameraAnalyticsSnapshots");

            migrationBuilder.DropTable(
                name: "CameraEvents");

            migrationBuilder.DropTable(
                name: "CameraUserAccesses");

            migrationBuilder.DropTable(
                name: "PtzPresets");

            migrationBuilder.DropTable(
                name: "RecordingChunks");

            migrationBuilder.DropTable(
                name: "RecordingSchedules");

            migrationBuilder.DropTable(
                name: "StreamSessions");

            migrationBuilder.DropTable(
                name: "SystemAnalyticsSnapshots");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "UserCameraLayouts");

            migrationBuilder.DropTable(
                name: "Recordings");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Cameras");

            migrationBuilder.DropTable(
                name: "StorageProfiles");
        }
    }
}
