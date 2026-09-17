using Main.Core.Models;

namespace Main.Core.Services
{
    public interface ITokenService
    {
        Task<(string accessToken, string refreshTokenPlain)> CreateTokenAsync(
            AppUser appUser,
            string ipAddress,
            string? deviceInfo = null,
            string? deviceId = null,
            bool rememberMe = false,
            CancellationToken cancellationToken = default);

        Task<(string newAccessToken, string newRefreshTokenPlain)> RefreshTokenAsync(
            string refreshTokenPlain,
            string ipAddress,
            string? deviceInfo = null,
            string? deviceId = null,
            CancellationToken cancellationToken = default);

        Task RevokeRefreshTokenAsync(
            string refreshTokenPlain,
            string ipAddress,
            CancellationToken cancellationToken = default);

        Task RevokeAllUserRefreshTokensAsync(
            string ownerId,
            string userType,
            string ipAddress,
            string? reason = null,
            CancellationToken cancellationToken = default);

        Task RevokeOtherDeviceTokensAsync(
            string ownerId,
            string userType,
            string currentDeviceId,
            string ipAddress,
            CancellationToken cancellationToken = default);
    }
}
