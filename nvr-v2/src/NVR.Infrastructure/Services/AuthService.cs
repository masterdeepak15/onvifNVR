using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NVR.Core.DTOs;
using NVR.Core.Entities;
using NVR.Infrastructure.Data;

namespace NVR.Infrastructure.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
        Task<AuthResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
        Task<AppUser> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
        Task LogoutAsync(string userId, CancellationToken ct = default);
        Task<AppUser?> GetUserByIdAsync(string userId, CancellationToken ct = default);
        Task<List<AppUser>> GetAllUsersAsync(CancellationToken ct = default);
        Task<AppUser> UpdateUserAsync(string userId, UpdateUserRequest request, CancellationToken ct = default);
        Task DeleteUserAsync(string userId, CancellationToken ct = default);
    }

    public class UpdateUserRequest
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? Role { get; set; }
        public bool? IsActive { get; set; }
    }

    public class AuthService : IAuthService
    {
        private readonly NvrDbContext _db;
        private readonly IConfiguration _config;

        public AuthService(NvrDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive, ct)
                ?? throw new UnauthorizedAccessException("Invalid credentials");

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid credentials");

            user.LastLoginAt = DateTime.UtcNow;
            var (accessToken, refreshToken, expiry) = GenerateTokens(user);
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);
            await _db.SaveChangesAsync(ct);

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiry,
                User = MapToDto(user)
            };
        }

        public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u =>
                u.RefreshToken == refreshToken &&
                u.RefreshTokenExpiry > DateTime.UtcNow &&
                u.IsActive, ct)
                ?? throw new UnauthorizedAccessException("Invalid or expired refresh token");

            var (accessToken, newRefreshToken, expiry) = GenerateTokens(user);
            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);
            await _db.SaveChangesAsync(ct);

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = expiry,
                User = MapToDto(user)
            };
        }

        public async Task<AppUser> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            if (await _db.Users.AnyAsync(u => u.Username == request.Username, ct))
                throw new InvalidOperationException("Username already exists");

            var user = new AppUser
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = request.Role
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
            return user;
        }

        public async Task LogoutAsync(string userId, CancellationToken ct = default)
        {
            var user = await _db.Users.FindAsync(new[] { userId }, ct);
            if (user == null) return;
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;
            await _db.SaveChangesAsync(ct);
        }

        public Task<AppUser?> GetUserByIdAsync(string userId, CancellationToken ct = default)
            => _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

        public Task<List<AppUser>> GetAllUsersAsync(CancellationToken ct = default)
            => _db.Users.ToListAsync(ct);

        public async Task<AppUser> UpdateUserAsync(string userId, UpdateUserRequest request, CancellationToken ct = default)
        {
            var user = await _db.Users.FindAsync(new[] { userId }, ct)
                ?? throw new KeyNotFoundException("User not found");

            if (request.Email != null) user.Email = request.Email;
            if (request.Password != null) user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
            if (request.Role != null) user.Role = request.Role;
            if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

            await _db.SaveChangesAsync(ct);
            return user;
        }

        public async Task DeleteUserAsync(string userId, CancellationToken ct = default)
        {
            var user = await _db.Users.FindAsync(new[] { userId }, ct)
                ?? throw new KeyNotFoundException("User not found");
            _db.Users.Remove(user);
            await _db.SaveChangesAsync(ct);
        }

        private (string accessToken, string refreshToken, DateTime expiry) GenerateTokens(AppUser user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiry = DateTime.UtcNow.AddHours(1);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expiry,
                signingCredentials: creds
            );

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
            var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            return (accessToken, refreshToken, expiry);
        }

        private UserDto MapToDto(AppUser user) => new()
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            LastLoginAt = user.LastLoginAt
        };
    }
}
