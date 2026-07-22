using POS.Domain.Entities;

namespace POS.Application.Interfaces;

public interface IJwtTokenService
{
    /// <summary>JWT POS multi-tenant (claim <c>tenant_id</c>, <c>business_type</c> y roles <c>Tenant.*</c>).</summary>
    string CreateToken(
        ApplicationUser user,
        string businessType,
        IReadOnlyList<string> tenantRoles,
        CancellationToken cancellationToken = default);

    /// <summary>JWT consola plataforma: sin <c>tenant_id</c>; incluye <c>is_platform</c> y roles <c>Platform.*</c>.</summary>
    string CreatePlatformToken(
        ApplicationUser user,
        IReadOnlyList<string> platformRoles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// JWT POS en contexto de un tenant objetivo para soporte; incluye <c>impersonation=true</c> y <c>Tenant.Admin</c>.
    /// </summary>
    string CreateImpersonationToken(
        ApplicationUser platformOperator,
        string targetTenantId,
        string businessType,
        string reason,
        int ttlMinutes,
        CancellationToken cancellationToken = default);
}
