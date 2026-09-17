using Main.Core.Models;
using Main.Core.Models.Dtos.Auth;
using Main.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Main.Application
{
    public class AppAuthService : IAppAuthService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ITokenService _tokenService;

        public AppAuthService(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            ITokenService tokenService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, string ipAddress, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                throw new InvalidOperationException("البريد الإلكتروني وكلمة المرور مطلوبان.");

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                throw new UnauthorizedAccessException("Invalid email or password.");

            // Goes through SignInManager (not UserManager.CheckPasswordAsync) so the
            // Lockout policy configured in IdentityExtension actually applies: failed
            // attempts are counted and the account gets locked out after too many.
            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

            if (result.IsLockedOut)
                throw new UnauthorizedAccessException("تم قفل الحساب مؤقتًا بسبب محاولات دخول خاطئة متكررة. حاول لاحقًا.");

            if (!result.Succeeded)
                throw new UnauthorizedAccessException("Invalid email or password.");

            var (accessToken, refreshToken) = await _tokenService.CreateTokenAsync(
                user,
                ipAddress,
                request.DeviceInfo,
                request.DeviceId,
                request.RememberMe,
                cancellationToken);

            var roles = await _userManager.GetRolesAsync(user);

            return new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserId = user.Id,
                UserName = user.UserName ?? user.Email,
                Email = user.Email,
                Role = roles.FirstOrDefault(),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(60)
            };
        }
    }
}
