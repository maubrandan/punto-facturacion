using POS.Application.Contracts.Platform;

namespace POS.Application.Interfaces.Platform;

/// <summary>Consultas de catálogo global (tenants) para operadores de plataforma; expuestas en <c>api/platform/tenants</c> con policy <c>Platform.User</c>.</summary>
public interface IPlatformDirectoryQuery
{
    Task<IReadOnlyList<TenantSummaryDto>> ListAllTenantsAsync(
        CancellationToken cancellationToken = default);

    Task<TenantDirectoryPageDto> ListTenantsPageAsync(
        int page,
        int pageSize,
        TenantListFilter? filter = null,
        CancellationToken cancellationToken = default);

    Task<TenantDetailDto?> GetTenantByIdAsync(string tenantId, CancellationToken cancellationToken = default);
}
