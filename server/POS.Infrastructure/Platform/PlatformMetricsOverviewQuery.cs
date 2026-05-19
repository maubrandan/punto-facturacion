using Microsoft.EntityFrameworkCore;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Platform;

public sealed class PlatformMetricsOverviewQuery : IPlatformMetricsOverviewQuery
{
    private readonly ApplicationDbContext _db;

    public PlatformMetricsOverviewQuery(ApplicationDbContext db) => _db = db;

    public async Task<PlatformMetricsOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var fromUtc = nowUtc.AddHours(-24);

        var totalTenants = await _db.Tenants.AsNoTracking().CountAsync(cancellationToken);
        var activeTenants = await _db.Tenants.AsNoTracking().CountAsync(x => x.Status == TenantStatus.Active, cancellationToken);
        var suspendedTenants =
            await _db.Tenants.AsNoTracking().CountAsync(x => x.Status == TenantStatus.Suspended, cancellationToken);
        var closedTenants = await _db.Tenants.AsNoTracking().CountAsync(x => x.Status == TenantStatus.Closed, cancellationToken);
        var blockedTenantUsers = await _db.Users
            .AsNoTracking()
            .CountAsync(x => x.AccountKind == UserAccountKind.TenantUser && x.BlockedByPlatform, cancellationToken);
        var recentAuditEvents = await _db.PlatformAuditEvents
            .AsNoTracking()
            .CountAsync(x => x.CreatedAtUtc >= fromUtc, cancellationToken);

        return new PlatformMetricsOverviewDto
        {
            TotalTenants = totalTenants,
            ActiveTenants = activeTenants,
            SuspendedTenants = suspendedTenants,
            ClosedTenants = closedTenants,
            BlockedTenantUsers = blockedTenantUsers,
            RecentAuditEvents = recentAuditEvents
        };
    }
}
