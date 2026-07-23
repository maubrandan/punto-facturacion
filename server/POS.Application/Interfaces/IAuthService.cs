using POS.Application.Common;
using POS.Application.Contracts.Auth;

namespace POS.Application.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Login solo para cuentas de consola de plataforma con roles <c>Platform.*</c>.</summary>
    Task<Result<AuthResponse>> PlatformLoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>Confirma el email con el token enviado por correo (público, sin JWT).</summary>
    Task<Result<AuthMessageResponse>> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Restablece la contraseña con el token enviado por correo (público, sin JWT).</summary>
    Task<Result<AuthMessageResponse>> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Autogestión: solicita correo de restablecimiento (público).
    /// Siempre confirma recepción; no revela si el email existe.
    /// </summary>
    Task<Result<AuthMessageResponse>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);
}
