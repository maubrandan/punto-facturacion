using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Platform;

public sealed class TenantSubscriptionQuery : ITenantSubscriptionQuery
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly PlatformTenantSubscriptionService _platformSubscriptions;

    public TenantSubscriptionQuery(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        PlatformTenantSubscriptionService platformSubscriptions)
    {
        _db = db;
        _currentUser = currentUser;
        _platformSubscriptions = platformSubscriptions;
    }

    public async Task<Result<TenantSubscriptionDto>> GetForCurrentTenantAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId?.Trim();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<TenantSubscriptionDto>.Failure(
                "subscription.tenant_required",
                "Se requiere un tenant en el contexto.");
        }

        // Aislamiento: solo el tenant del JWT; nunca aceptar tenantId de ruta/query.
        var exists = await _db.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, cancellationToken);
        if (!exists)
            return Result<TenantSubscriptionDto>.Failure("tenant.not_found", "No existe el tenant.");

        var row = await _platformSubscriptions.EnsureSubscriptionRowAsync(tenantId, cancellationToken);
        var entitlement = await _db.TenantEntitlements
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId, cancellationToken);
        var matched = TenantEntitlementsMapper.FromRow(entitlement).MatchedPlanCode;
        return Result<TenantSubscriptionDto>.Ok(TenantSubscriptionMapper.ToDto(row, matched));
    }
}
