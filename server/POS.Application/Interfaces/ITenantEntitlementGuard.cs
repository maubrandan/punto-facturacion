using POS.Application.Common;

namespace POS.Application.Interfaces;

/// <summary>
/// Validación contra <c>TenantEntitlement</c> en operaciones costosas multi-tenant (Fase 9).
/// </summary>
public interface ITenantEntitlementGuard
{
    Task<Result<object?>> EnsureCanCreateProductAsync(CancellationToken cancellationToken = default);

    Task<Result<object?>> EnsureCanRecordSaleAsync(CancellationToken cancellationToken = default);

    /// <summary>Uso cuando un caso de alta agrega un usuario <c>TenantUser</c> al tenant actual u otro <paramref name="tenantId"/>.</summary>
    Task<Result<object?>> EnsureCanAddTenantUserAsync(string tenantId, CancellationToken cancellationToken = default);
}
