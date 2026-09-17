using Main.API.Errors;
using Main.Core.Models.Dtos.Auth;
using Main.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Main.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private readonly IAppAuthService _appAuthService;
        private readonly ITokenService _tokenService;

        public AuthController(IAppAuthService appAuthService, ITokenService tokenService)
        {
            _appAuthService = appAuthService;
            _tokenService = tokenService;
        }

        [HttpPost("login")]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            var response = await _appAuthService.LoginAsync(request, ipAddress, cancellationToken);

            return Ok(new ApiResponse(200, "تم تسجيل الدخول بنجاح.", response));
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            var (newAccessToken, newRefreshToken) = await _tokenService.RefreshTokenAsync(
                request.RefreshToken,
                ipAddress,
                request.DeviceInfo,
                request.DeviceId,
                cancellationToken);

            var response = new AuthResponseDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            };

            return Ok(new ApiResponse(200, "تم تحديث الرمز بنجاح.", response));
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request, CancellationToken cancellationToken)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken, ipAddress, cancellationToken);

            return Ok(new ApiResponse(200, "تم تسجيل الخروج بنجاح."));
        }
    }
}
