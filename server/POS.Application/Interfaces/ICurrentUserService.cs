namespace POS.Application.Interfaces;

/// <summary>
/// Usuario y tenant del contexto actual (típicamente request). El DbContext usa <see cref="TenantId"/> para el filtro global multi-tenant.
/// </summary>
public interface ICurrentUserService
{
    string? TenantId { get; }

    string? UserId { get; }

    /// <summary>
    /// True si el request tiene claim <c>is_platform=true</c> (JWT emitido por <c>POST /api/platform/auth/login</c> o tests).
    /// </summary>
    bool IsPlatformContext { get; }

    /// <summary>True si el JWT es de suplantación soporte (<c>impersonation=true</c>, Fase 7).</summary>
    bool IsImpersonationContext { get; }
}
