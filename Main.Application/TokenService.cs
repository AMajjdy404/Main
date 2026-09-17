using Main.Core.Interfaces;
using Main.Core.Models;
using Main.Core.Models.Settings;
using Main.Core.Services;
using Main.Infrastructure.ReposAndSpecs.RefreshTokenSpecs;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Main.Application
{
    public class TokenService : ITokenService
    {
        private const string AppUserType = "AppUser";
        private const int MaxActiveSessionsPerUser = 5;

        private readonly JwtSettings _jwtSettings;
        private readonly IUnitOfWork _uow;
        private readonly UserManager<AppUser> _userManager;

        public TokenService(
            IOptions<JwtSettings> jwtOptions,
            IUnitOfWork uow,
            UserManager<AppUser> userManager)
        {
            _jwtSettings = jwtOptions.Value;
            _uow = uow;
            _userManager = userManager;
        }

        public async Task<(string accessToken, string refreshTokenPlain)> CreateTokenAsync(
            AppUser appUser,
            string ipAddress,
            string? deviceInfo = null,
            string? deviceId = null,
            bool rememberMe = false,
            CancellationToken cancellationToken = default)
        {
            var claims = await BuildClaimsForAppUserAsync(appUser);
            var accessToken = GenerateJwtToken(claims);

            await EnforceSessionLimitAsync(appUser.Id.ToString(), AppUserType, ipAddress, cancellationToken);

            var (plainRefreshToken, refreshTokenEntity) = GenerateRefreshTokenEntity(
                ownerId: appUser.Id.ToString(),
                userType: AppUserType,
                ipAddress: ipAddress,
                deviceInfo: deviceInfo,
                deviceId: deviceId,
                rememberMe: rememberMe);

            await _uow.Repository<RefreshToken>().AddAsync(refreshTokenEntity, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);

            return (accessToken, plainRefreshToken);
        }

        public async Task<(string newAccessToken, string newRefreshTokenPlain)> RefreshTokenAsync(
            string refreshTokenPlain,
            string ipAddress,
            string? deviceInfo = null,
            string? deviceId = null,
            CancellationToken cancellationToken = default)
        {
            var normalizedToken = NormalizeToken(refreshTokenPlain);
            if (string.IsNullOrWhiteSpace(normalizedToken))
                throw new SecurityTokenException("Invalid refresh token");

            var tokenHash = ComputeSha256Hash(normalizedToken);
            var repo = _uow.Repository<RefreshToken>();

            var existing = await repo.FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);

            if (existing == null)
                throw new SecurityTokenException("Invalid refresh token");

            // reuse detection
            if (existing.IsRevoked || existing.IsCompromised)
            {
                await MarkTokenFamilyAsCompromisedAsync(
                    existing.OwnerId,
                    existing.UserType,
                    ipAddress,
                    "Refresh token reuse detected",
                    cancellationToken);

                throw new SecurityTokenException("Refresh token reuse detected");
            }

            if (existing.IsExpired)
                throw new SecurityTokenException("Refresh token expired");

            // device binding
            if (!string.IsNullOrWhiteSpace(existing.DeviceId) &&
                !string.IsNullOrWhiteSpace(deviceId) &&
                !string.Equals(existing.DeviceId, deviceId, StringComparison.Ordinal))
            {
                await MarkTokenFamilyAsCompromisedAsync(
                    existing.OwnerId,
                    existing.UserType,
                    ipAddress,
                    "Device mismatch detected",
                    cancellationToken);

                throw new SecurityTokenException("Device mismatch detected");
            }

            var (newPlainToken, newEntity) = GenerateRefreshTokenEntity(
                ownerId: existing.OwnerId,
                userType: existing.UserType,
                ipAddress: ipAddress,
                deviceInfo: deviceInfo,
                deviceId: deviceId ?? existing.DeviceId,
                rememberMe: existing.IsRememberMe);

            existing.IsRevoked = true;
            existing.RevokedAt = DateTime.UtcNow;
            existing.RevokedByIpAddress = ipAddress;
            existing.ReplacedByTokenHash = newEntity.TokenHash;

            repo.Update(existing);
            await repo.AddAsync(newEntity, cancellationToken);

            var claims = await BuildClaimsFromRefreshTokenOwnerAsync(existing);
            var newAccessToken = GenerateJwtToken(claims);

            await _uow.SaveChangesAsync(cancellationToken);

            return (newAccessToken, newPlainToken);
        }

        public async Task RevokeRefreshTokenAsync(string refreshTokenPlain, string ipAddress, CancellationToken cancellationToken = default)
        {
            var normalizedToken = NormalizeToken(refreshTokenPlain);
            if (string.IsNullOrWhiteSpace(normalizedToken))
                return;

            var tokenHash = ComputeSha256Hash(normalizedToken);
            var repo = _uow.Repository<RefreshToken>();

            var existing = await repo.FirstOrDefaultAsync(r => r.TokenHash == tokenHash, cancellationToken);

            if (existing == null || existing.IsRevoked)
                return;

            existing.IsRevoked = true;
            existing.RevokedAt = DateTime.UtcNow;
            existing.RevokedByIpAddress = ipAddress;

            repo.Update(existing);
            await _uow.SaveChangesAsync(cancellationToken);
        }

        public async Task RevokeAllUserRefreshTokensAsync(
            string ownerId,
            string userType,
            string ipAddress,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            var repo = _uow.Repository<RefreshToken>();
            var activeTokens = await repo.ListAsync(new ActiveRefreshTokensByOwnerSpecification(ownerId, userType), cancellationToken);

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIpAddress = ipAddress;
                token.CompromisedReason = reason;
            }

            repo.UpdateRange(activeTokens);
            await _uow.SaveChangesAsync(cancellationToken);
        }

        public async Task RevokeOtherDeviceTokensAsync(
            string ownerId,
            string userType,
            string currentDeviceId,
            string ipAddress,
            CancellationToken cancellationToken = default)
        {
            var repo = _uow.Repository<RefreshToken>();
            var otherTokens = await repo.ListAsync(
                new ActiveRefreshTokensExcludingDeviceSpecification(ownerId, userType, currentDeviceId), cancellationToken);

            foreach (var token in otherTokens)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIpAddress = ipAddress;
            }

            repo.UpdateRange(otherTokens);
            await _uow.SaveChangesAsync(cancellationToken);
        }

        private async Task<List<Claim>> BuildClaimsForAppUserAsync(AppUser appUser)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, appUser.Id.ToString()),
                new Claim(ClaimTypes.Email, appUser.Email ?? string.Empty),
                new Claim(ClaimTypes.Name, appUser.UserName ?? string.Empty),
                new Claim("UserType", AppUserType)
            };

            var roles = await _userManager.GetRolesAsync(appUser);
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            return claims;
        }

        private async Task<List<Claim>> BuildClaimsFromRefreshTokenOwnerAsync(RefreshToken token)
        {
            if (token.UserType != AppUserType)
                throw new SecurityTokenException("Invalid user type");

            var user = await _userManager.FindByIdAsync(token.OwnerId);
            if (user == null)
                throw new SecurityTokenException("Invalid token owner");

            return await BuildClaimsForAppUserAsync(user);
        }

        private string GenerateJwtToken(List<Claim> claims)
        {
            var authKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.ValidIssuer,
                audience: _jwtSettings.ValidAudience,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenDurationInMinutes),
                claims: claims,
                signingCredentials: new SigningCredentials(authKey, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private (string plainToken, RefreshToken entity) GenerateRefreshTokenEntity(
            string ownerId,
            string userType,
            string ipAddress,
            string? deviceInfo,
            string? deviceId,
            bool rememberMe)
        {
            var bytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);

            var plainToken = NormalizeToken(Convert.ToBase64String(bytes));
            var hash = ComputeSha256Hash(plainToken);

            var days = rememberMe
                ? _jwtSettings.RefreshTokenRememberMeDurationInDays
                : _jwtSettings.RefreshTokenDurationInDays;

            var entity = new RefreshToken
            {
                TokenHash = hash,
                OwnerId = ownerId,
                UserType = userType,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(days),
                RemoteIpAddress = ipAddress,
                DeviceInfo = deviceInfo,
                DeviceId = deviceId,
                IsRememberMe = rememberMe
            };

            return (plainToken, entity);
        }

        private async Task EnforceSessionLimitAsync(string ownerId, string userType, string ipAddress, CancellationToken cancellationToken)
        {
            var repo = _uow.Repository<RefreshToken>();
            var activeTokens = await repo.ListAsync(new ActiveRefreshTokensByOwnerSpecification(ownerId, userType), cancellationToken);

            var extraCount = (activeTokens.Count + 1) - MaxActiveSessionsPerUser;
            if (extraCount <= 0)
                return;

            var tokensToRevoke = activeTokens.Take(extraCount).ToList();

            foreach (var token in tokensToRevoke)
            {
                token.IsRevoked = true;
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedByIpAddress = ipAddress;
            }

            repo.UpdateRange(tokensToRevoke);
        }

        private async Task MarkTokenFamilyAsCompromisedAsync(
            string ownerId,
            string userType,
            string ipAddress,
            string reason,
            CancellationToken cancellationToken)
        {
            var repo = _uow.Repository<RefreshToken>();
            var relatedTokens = await repo.ListAsync(new NonCompromisedRefreshTokensByOwnerSpecification(ownerId, userType), cancellationToken);

            foreach (var token in relatedTokens)
            {
                token.IsCompromised = true;
                token.CompromisedAt = DateTime.UtcNow;
                token.CompromisedReason = reason;

                if (!token.IsRevoked)
                {
                    token.IsRevoked = true;
                    token.RevokedAt = DateTime.UtcNow;
                    token.RevokedByIpAddress = ipAddress;
                }
            }

            repo.UpdateRange(relatedTokens);
            await _uow.SaveChangesAsync(cancellationToken);
        }

        private static string ComputeSha256Hash(string input)
        {
            var normalized = NormalizeToken(input);
            var bytes = Encoding.UTF8.GetBytes(normalized);
            var hash = SHA256.HashData(bytes);
            return Convert.ToBase64String(hash);
        }

        private static string NormalizeToken(string? token)
        {
            return token?.Trim().TrimEnd('=') ?? string.Empty;
        }
    }
}
