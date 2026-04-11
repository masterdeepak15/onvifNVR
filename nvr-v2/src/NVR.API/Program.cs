using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NVR.API.Hubs;
using NVR.Core.Interfaces;
using NVR.Infrastructure.Data;
using NVR.Infrastructure.Services;
using NVR.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// DATABASE
// ============================================================
var dbProvider = builder.Configuration["Database:Provider"] ?? "SQLite";

if (dbProvider == "SqlServer")
{
    // SQL Server: reads ConnectionStrings__SqlServer env var first,
    // falls back to DefaultConnection only as a last resort.
    var sqlServerConn = builder.Configuration.GetConnectionString("SqlServer")
        ?? builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "A SQL Server connection string is required when Database:Provider=SqlServer. " +
            "Set ConnectionStrings__SqlServer environment variable.");

    builder.Services.AddDbContext<NvrDbContext>(options =>
        options.UseSqlServer(sqlServerConn, sql =>
        {
            sql.MigrationsAssembly("NVR.Infrastructure");
            sql.CommandTimeout(60);
            sql.EnableRetryOnFailure(3);
        }));
}
else
{
    // SQLite (default): reads ConnectionStrings__DefaultConnection.
    // Default path: /var/nvr/data/nvr.db (inside Docker volume).
    // Local dev default: nvr.db in working directory.
    var sqliteConn = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=nvr.db";

    builder.Services.AddDbContext<NvrDbContext>(options =>
        options.UseSqlite(sqliteConn, sqlite =>
            sqlite.MigrationsAssembly("NVR.Infrastructure")));
}

// ============================================================
// JWT — MUST BE CONFIGURED BEFORE SignalR
// ============================================================
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is required in configuration");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };

        // SignalR auth: extract JWT from query string.
        // Client connects: wss://host/hubs/nvr?access_token=<JWT>
        // Browsers cannot send custom headers on WebSocket upgrade — this is the standard pattern.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return System.Threading.Tasks.Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<Microsoft.Extensions.Logging.ILogger<Program>>();
                logger.LogWarning("Hub auth failed: {Error}", context.Exception.Message);
                return System.Threading.Tasks.Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AtLeastOperator", p => p.RequireRole("Admin", "Operator"));
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
});

// ============================================================
// CORS
// ============================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("NvrPolicy", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?? new[] { "http://localhost:3000", "http://localhost:5173", "http://localhost:8080" };

        // Filter out empty entries (env vars like CORS_ORIGIN_2= produce empty strings)
        origins = origins.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();

        policy
            .WithOrigins(origins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();  // Required for SignalR
    });
});

// ============================================================
// SIGNALR
// ============================================================
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB — for frame data
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// ============================================================
// STORAGE PROVIDERS (pluggable)
// ============================================================
builder.Services.AddTransient<LocalStorageProvider>();
builder.Services.AddTransient<NasSmbStorageProvider>();
builder.Services.AddTransient<S3StorageProvider>();
builder.Services.AddTransient<AzureBlobStorageProvider>();
builder.Services.AddSingleton<IStorageProviderFactory, StorageProviderFactory>();

// ============================================================
// HTTP CLIENT (for ONVIF SOAP calls)
// ============================================================
builder.Services.AddHttpClient("onvif", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
})
.ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler
{
    // Accept self-signed certs — cameras use them universally
    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
});

// ============================================================
// APPLICATION SERVICES
// ============================================================
builder.Services.AddScoped<IOnvifService, OnvifService>();
builder.Services.AddSingleton<IRtspStreamService, RtspStreamService>();
builder.Services.AddSingleton<IRecordingService, RecordingService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICameraAccessService, CameraAccessService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddSingleton<INvrHubEventEmitter, NvrHubEventEmitter>();
builder.Services.AddHostedService<NvrMaintenanceService>();

// ============================================================
// API + SWAGGER
// ============================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NVR API v2",
        Version = "v2",
        Description = "Full ONVIF NVR System — Streaming | PTZ | Recording | Analytics | Role-Based Access"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "JWT Bearer token. Format: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<NvrDbContext>("database");

var app = builder.Build();

// ============================================================
// MIDDLEWARE PIPELINE
// ============================================================

// Expose Swagger in all environments (remove the Production block if unwanted)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NVR API v2");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseCors("NvrPolicy");
app.UseAuthentication();
app.UseAuthorization();

// ============================================================
// ROUTES
// ============================================================
app.MapControllers();
app.MapHub<NvrHub>("/hubs/nvr");
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Redirect("/swagger"));

// ============================================================
// DATABASE MIGRATE + SEED
// ============================================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NvrDbContext>();

    // For SQLite: ensure the directory exists before EF tries to create the file
    if (dbProvider != "SqlServer")
    {
        var connStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
        var match = System.Text.RegularExpressions.Regex.Match(connStr, @"Data Source=([^;]+)");
        if (match.Success)
        {
            var dbPath = match.Groups[1].Value.Trim();
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }
    }

    await db.Database.MigrateAsync();

    if (!await db.Users.AnyAsync())
    {
        db.Users.AddRange(
            new NVR.Core.Entities.AppUser
            {
                Username = "admin",
                Email = "admin@nvr.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Role = "Admin"
            },
            new NVR.Core.Entities.AppUser
            {
                Username = "operator",
                Email = "operator@nvr.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Operator@123"),
                Role = "Operator"
            },
            new NVR.Core.Entities.AppUser
            {
                Username = "viewer",
                Email = "viewer@nvr.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Viewer@123"),
                Role = "Viewer"
            }
        );

        db.StorageProfiles.Add(new NVR.Core.Entities.StorageProfile
        {
            Name = "Local Storage",
            Type = "Local",
            IsDefault = true,
            BasePath = "/var/nvr/recordings",
            RetentionDays = 30,
            AutoDeleteEnabled = true,
            MaxStorageBytes = 500L * 1024 * 1024 * 1024
        });

        db.SystemSettings.AddRange(
            new NVR.Core.Entities.SystemSetting { Key = "system.name",                        Value = "NVR System", Category = "General"   },
            new NVR.Core.Entities.SystemSetting { Key = "recording.default_chunk_seconds",     Value = "60",         Category = "Recording" },
            new NVR.Core.Entities.SystemSetting { Key = "recording.default_bitrate_kbps",      Value = "2000",       Category = "Recording" },
            new NVR.Core.Entities.SystemSetting { Key = "storage.low_space_warning_percent",   Value = "85",         Category = "Storage"   },
            new NVR.Core.Entities.SystemSetting { Key = "stream.max_cameras",                  Value = "64",         Category = "Streaming" },
            new NVR.Core.Entities.SystemSetting { Key = "stream.default_fps",                  Value = "15",         Category = "Streaming" },
            new NVR.Core.Entities.SystemSetting { Key = "analytics.hourly_snapshot_enabled",   Value = "true",       Category = "Analytics" }
        );

        await db.SaveChangesAsync();
        Console.WriteLine("Seeded default users: admin/Admin@123 | operator/Operator@123 | viewer/Viewer@123");
        Console.WriteLine("IMPORTANT: Change default passwords immediately after first login.");
    }
}

Console.WriteLine($"NVR System v2 running. Database: {dbProvider}");
Console.WriteLine("   Swagger : /swagger");
Console.WriteLine("   SignalR : /hubs/nvr?access_token=<JWT>");
Console.WriteLine("   Health  : /health");

app.Run();
