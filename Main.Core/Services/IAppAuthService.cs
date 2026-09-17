using Main.Core.Models.Dtos.Auth;

namespace Main.Core.Services
{
    public interface IAppAuthService
    {
        Task<AuthResponseDto> LoginAsync(LoginRequestDto request, string ipAddress, CancellationToken cancellationToken = default);
    }
}
