using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Inventory;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Inventory;

public sealed class InventoryQueryService : IInventoryQueryService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public InventoryQueryService(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<StockLotResponse>>> GetLotsForProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId?.Trim();
        if (string.IsNullOrEmpty(tenantId))
        {
            return Result<IReadOnlyList<StockLotResponse>>.Failure(
                "stock.tenant_required",
                "No se pudo determinar el comercio (tenant).");
        }

        var productExists = await _db.Products
            .AsNoTracking()
            .AnyAsync(p => p.Id == productId && p.TenantId == tenantId, cancellationToken);

        if (!productExists)
            return Result<IReadOnlyList<StockLotResponse>>.Failure("product.not_found", "Producto no encontrado.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lots = await _db.StockLots
            .AsNoTracking()
            .Where(l => l.ProductId == productId)
            .OrderBy(l => l.ExpirationDate)
            .ThenBy(l => l.LotNumber)
            .Select(l => new StockLotResponse
            {
                Id = l.Id,
                ProductId = l.ProductId,
                LotNumber = l.LotNumber,
                ExpirationDate = l.ExpirationDate,
                Quantity = l.Quantity,
                IsExpired = l.ExpirationDate < today
            })
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<StockLotResponse>>.Ok(lots);
    }

    public async Task<Result<PagedStockMovementsResponse>> GetMovementsAsync(
        Guid? productId,
        int page,
        int pageSize,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _db.StockMovements.AsNoTracking().AsQueryable();
        if (productId is not null)
            query = query.Where(m => m.ProductId == productId);

        if (fromUtc is not null)
        {
            var from = NormalizeUtc(fromUtc.Value);
            query = query.Where(m => m.CreatedAt >= from);
        }

        if (toUtc is not null)
        {
            var to = NormalizeUtc(toUtc.Value);
            query = query.Where(m => m.CreatedAt <= to);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(
                _db.Products.AsNoTracking(),
                m => m.ProductId,
                p => p.Id,
                (m, p) => new { m, p.Name })
            .ToListAsync(cancellationToken);

        var mapped = items.Select(x => new StockMovementResponse
        {
            Id = x.m.Id,
            ProductId = x.m.ProductId,
            ProductName = x.Name,
            Type = x.m.Type.ToString(),
            QuantityDelta = x.m.QuantityDelta,
            QuantityAfter = x.m.QuantityAfter,
            StockLotId = x.m.StockLotId,
            LotNumberSnapshot = x.m.LotNumberSnapshot,
            ExpirationSnapshot = x.m.ExpirationSnapshot,
            ReasonCode = x.m.ReasonCode,
            ReasonNote = x.m.ReasonNote,
            ReferenceId = x.m.ReferenceId,
            CreatedAt = x.m.CreatedAt
        }).ToList();

        return Result<PagedStockMovementsResponse>.Ok(
            new PagedStockMovementsResponse
            {
                Items = mapped,
                Page = page,
                PageSize = pageSize,
                TotalCount = total
            });
    }

    public Task<Result<IReadOnlyList<AdjustmentReasonOptionResponse>>> GetAdjustmentReasonsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = StockAdjustmentReasonCodes.Options()
            .Select(o => new AdjustmentReasonOptionResponse { Code = o.Code, Label = o.Label })
            .ToList();
        return Task.FromResult(
            Result<IReadOnlyList<AdjustmentReasonOptionResponse>>.Ok(options));
    }

    public async Task<Result<ExpiryAlertsResponse>> GetExpiryAlertsAsync(
        int? withinDays = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId?.Trim();
        if (string.IsNullOrEmpty(tenantId))
        {
            return Result<ExpiryAlertsResponse>.Failure(
                "stock.tenant_required",
                "No se pudo determinar el comercio (tenant).");
        }

        var days = withinDays ?? PharmacyExpiryAlertRules.DefaultWithinDays;
        if (days < PharmacyExpiryAlertRules.MinWithinDays)
            days = PharmacyExpiryAlertRules.MinWithinDays;
        if (days > PharmacyExpiryAlertRules.MaxWithinDays)
            days = PharmacyExpiryAlertRules.MaxWithinDays;

        var businessType = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.BusinessType)
            .FirstOrDefaultAsync(cancellationToken);

        var isFarmacia = !string.IsNullOrWhiteSpace(businessType)
            && BusinessTypeNames.IsKnown(businessType)
            && string.Equals(
                BusinessTypeNames.Normalize(businessType),
                BusinessTypeNames.Farmacia,
                StringComparison.Ordinal);

        if (!isFarmacia)
        {
            return Result<ExpiryAlertsResponse>.Ok(
                new ExpiryAlertsResponse
                {
                    Supported = false,
                    WithinDays = days,
                    Items = Array.Empty<ExpiryAlertItemResponse>()
                });
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(days);

        var lots = await _db.StockLots
            .AsNoTracking()
            .Where(l => l.Quantity > 0m && l.ExpirationDate <= horizon)
            .Join(
                _db.Products.AsNoTracking(),
                l => l.ProductId,
                p => p.Id,
                (l, p) => new { l, p.Name })
            .OrderBy(x => x.l.ExpirationDate)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.l.LotNumber)
            .Take(100)
            .ToListAsync(cancellationToken);

        var items = lots.Select(x =>
        {
            var daysTo = x.l.ExpirationDate.DayNumber - today.DayNumber;
            var status = daysTo < 0
                ? ExpiryAlertStatuses.Expired
                : ExpiryAlertStatuses.ExpiringSoon;

            return new ExpiryAlertItemResponse
            {
                StockLotId = x.l.Id,
                ProductId = x.l.ProductId,
                ProductName = x.Name,
                LotNumber = x.l.LotNumber,
                ExpirationDate = x.l.ExpirationDate,
                Quantity = x.l.Quantity,
                Status = status,
                DaysToExpiration = daysTo
            };
        }).ToList();

        return Result<ExpiryAlertsResponse>.Ok(
            new ExpiryAlertsResponse
            {
                Supported = true,
                WithinDays = days,
                Items = items
            });
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
