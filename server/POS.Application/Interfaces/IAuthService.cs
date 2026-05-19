using POS.Application.Common;
using POS.Application.Contracts.Auth;

namespace POS.Application.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Login solo para cuentas de consola de plataforma con roles <c>Platform.*</c>.</summary>
    Task<Result<AuthResponse>> PlatformLoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
