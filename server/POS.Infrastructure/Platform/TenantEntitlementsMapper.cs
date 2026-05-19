using POS.Application.Contracts.Platform;
using POS.Domain.Entities;

namespace POS.Infrastructure.Platform;

internal static class TenantEntitlementsMapper
{
    public static TenantEntitlementsDto FromRow(TenantEntitlement? row) =>
        row is null
            ? new TenantEntitlementsDto { SalesEnabled = true }
            : new TenantEntitlementsDto
            {
                MaxProducts = row.MaxProducts,
                MaxTenantUsers = row.MaxTenantUsers,
                SalesEnabled = row.SalesEnabled
            };
}
