using ShipMvp.Core.Abstractions;

namespace ShipMvp.Application.Identity;

public interface IAuthService : IScopedService
{
    Task<AuthResultDto> LoginAsync(LoginDto request, CancellationToken cancellationToken = default);
    //Task LogoutAsync(CancellationToken cancellationToken = default);
    //Task<AuthResultDto> RefreshTokenAsync(string token, CancellationToken cancellationToken = default);
}
