namespace POS.Application.Interfaces;

/// <summary>
/// Permite fijar el tenant del request cuando aún no hay JWT (p. ej. registro) o forzar el contexto para EF.
/// </summary>
public interface ICurrentUserTenantContext
{
    /// <summary>
    /// Si está definido, <see cref="ICurrentUserService.TenantId"/> lo prioriza frente a los claims.
    /// </summary>
    string? OverriddenTenantId { get; set; }
}
