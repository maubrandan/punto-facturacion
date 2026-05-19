using POS.Domain.Entities;

namespace POS.Application.Interfaces;

public interface IJwtTokenService
{
    /// <summary>JWT POS multi-tenant (claim <c>tenant_id</c>).</summary>
    string CreateToken(ApplicationUser user, CancellationToken cancellationToken = default);

    /// <summary>JWT consola plataforma: sin <c>tenant_id</c>; incluye <c>is_platform</c> y roles <c>Platform.*</c>.</summary>
    string CreatePlatformToken(
        ApplicationUser user,
        IReadOnlyList<string> platformRoles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// JWT POS en contexto de un tenant objetivo para soporte; incluye <c>impersonation=true</c>, sin <c>is_platform</c>.
    /// </summary>
    string CreateImpersonationToken(
        ApplicationUser platformOperator,
        string targetTenantId,
        string reason,
        int ttlMinutes,
        CancellationToken cancellationToken = default);
}
