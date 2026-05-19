using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Platform;

namespace POS.Infrastructure.Entitlements;

public sealed class TenantEntitlementGuard : ITenantEntitlementGuard
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public TenantEntitlementGuard(ApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<Result<object?>> EnsureCanCreateProductAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _current.TenantId?.Trim();
        if (string.IsNullOrEmpty(tenantId))
        {
            return Result<object?>.Failure(
                "entitlement.tenant_required",
                "No se pudo determinar el negocio (tenant).");
        }

        return await EvaluateProductQuotaAsync(tenantId, cancellationToken);
    }

    public async Task<Result<object?>> EnsureCanRecordSaleAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _current.TenantId?.Trim();
        if (string.IsNullOrEmpty(tenantId))
        {
            return Result<object?>.Failure(
                "entitlement.tenant_required",
                "No se pudo determinar el negocio (tenant).");
        }

        var row = await _db.Set<TenantEntitlement>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId, cancellationToken);
        var salesEnabled = TenantEntitlementsMapper.FromRow(row).SalesEnabled;
        if (!salesEnabled)
        {
            return Result<object?>.Failure(
                "entitlement.sales_disabled",
                "Ventas deshabilitadas para este negocio por política de plataforma.");
        }

        return Result<object?>.Ok(null);
    }

    /// <inheritdoc />
    public async Task<Result<object?>> EnsureCanAddTenantUserAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var tid = tenantId.Trim();
        if (string.IsNullOrEmpty(tid))
        {
            return Result<object?>.Failure(
                "entitlement.tenant_invalid",
                "tenantId obligatorio.");
        }

        var row = await _db.Set<TenantEntitlement>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tid, cancellationToken);

        var max = TenantEntitlementsMapper.FromRow(row).MaxTenantUsers;
        if (max is null)
            return Result<object?>.Ok(null);

        var count = await _db.Set<ApplicationUser>().CountAsync(
            u => u.TenantId == tid && u.AccountKind == UserAccountKind.TenantUser,
            cancellationToken);

        if (count >= max.Value)
        {
            return Result<object?>.Failure(
                "entitlement.user_limit_reached",
                $"Se alcanzó el límite de usuarios ({max.Value}) para este negocio.");
        }

        return Result<object?>.Ok(null);
    }

    /// <remarks>
    /// Conteo dentro del tenant actual mediante filtro EF del contexto POS (productos tienen TenantId consistente por claim).
    /// </remarks>
    private async Task<Result<object?>> EvaluateProductQuotaAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var row = await _db.Set<TenantEntitlement>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId, cancellationToken);

        var max = TenantEntitlementsMapper.FromRow(row).MaxProducts;
        if (max is null)
            return Result<object?>.Ok(null);

        var count = await _db.Products.CountAsync(cancellationToken);
        if (count >= max.Value)
        {
            return Result<object?>.Failure(
                "entitlement.product_limit_reached",
                $"Se alcanzó el límite de productos ({max.Value}). Contacte a soporte para más capacidad.");
        }

        return Result<object?>.Ok(null);
    }
}
