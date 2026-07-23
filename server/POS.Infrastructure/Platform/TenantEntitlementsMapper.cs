using POS.Application.Contracts.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;

namespace POS.Infrastructure.Platform;

internal static class TenantEntitlementsMapper
{
    public static TenantEntitlementsDto FromRow(TenantEntitlement? row)
    {
        var caps = row is null
            ? new TenantEntitlementsDto { SalesEnabled = true }
            : new TenantEntitlementsDto
            {
                MaxProducts = row.MaxProducts,
                MaxTenantUsers = row.MaxTenantUsers,
                SalesEnabled = row.SalesEnabled
            };
        return TenantPlanPresets.WithMatchedPlan(caps);
    }

    public static TenantEntitlement ToNewRow(
        string tenantId,
        TenantEntitlementsDto caps,
        DateTime updatedAtUtc) =>
        new()
        {
            TenantId = tenantId,
            MaxProducts = caps.MaxProducts,
            MaxTenantUsers = caps.MaxTenantUsers,
            SalesEnabled = caps.SalesEnabled,
            UpdatedAtUtc = updatedAtUtc
        };

    public static void Apply(TenantEntitlement row, TenantEntitlementsDto caps, DateTime updatedAtUtc)
    {
        row.MaxProducts = caps.MaxProducts;
        row.MaxTenantUsers = caps.MaxTenantUsers;
        row.SalesEnabled = caps.SalesEnabled;
        row.UpdatedAtUtc = updatedAtUtc;
    }
}
