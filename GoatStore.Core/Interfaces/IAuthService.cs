using GoatStore.Core.DTOs;

namespace GoatStore.Core.Interfaces
{
    public interface IAuthService : IDisposable
    {
        Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
        Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    }
}