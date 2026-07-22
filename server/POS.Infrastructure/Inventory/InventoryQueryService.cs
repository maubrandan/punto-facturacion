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
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _db.StockMovements.AsNoTracking().AsQueryable();
        if (productId is not null)
            query = query.Where(m => m.ProductId == productId);

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
            Reason = x.m.Reason,
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
}
