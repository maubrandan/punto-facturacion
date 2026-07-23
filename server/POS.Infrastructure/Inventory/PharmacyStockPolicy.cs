using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Inventory;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Inventory;

public sealed class PharmacyStockPolicy : IStockPolicy
{
    public const string DefaultLotNumber = "DEFAULT";
    public static readonly DateOnly DefaultExpiration = new(2099, 12, 31);

    private readonly ApplicationDbContext _db;

    public PharmacyStockPolicy(ApplicationDbContext db) => _db = db;

    public string BusinessType => BusinessTypeNames.Farmacia;

    public Result<object?> ValidateQuantity(decimal quantity) =>
        StockQuantityRules.RequireWholePositive(quantity);

    public Result<object?> ValidateSaleLine(StockLineContext line)
    {
        // Lote opcional: si se omite, ApplySaleAsync reparte FEFO entre lotes no vencidos.
        return ValidateQuantity(line.Quantity);
    }

    public Result<object?> ValidatePurchaseLine(StockLineContext line)
    {
        var qty = ValidateQuantity(line.Quantity);
        if (!qty.IsSuccess)
            return qty;

        if (string.IsNullOrWhiteSpace(line.LotNumber))
            return Result<object?>.Failure("stock.lot_required", "Debe indicar el número de lote.");

        if (line.ExpirationDate is null)
            return Result<object?>.Failure("stock.expiration_required", "Debe indicar la fecha de vencimiento del lote.");

        return Result<object?>.Ok(null);
    }

    public Result<object?> ValidateAdjustment(StockAdjustContext ctx)
    {
        if (ctx.QuantityDelta == 0m)
            return Result<object?>.Failure("stock.adjustment", "El ajuste no puede ser cero.");

        var abs = Math.Abs(ctx.QuantityDelta);
        var qty = StockQuantityRules.RequireWholePositive(abs);
        if (!qty.IsSuccess)
            return qty;

        var reason = StockQuantityRules.RequireKnownAdjustmentReason(ctx.ReasonCode);
        if (!reason.IsSuccess)
            return reason;

        if (ctx.QuantityDelta < 0m)
        {
            if (ctx.StockLotId is null || ctx.StockLotId == Guid.Empty)
            {
                return Result<object?>.Failure(
                    "stock.lot_required",
                    "Debe indicar el lote a descontar.");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(ctx.LotNumber))
                return Result<object?>.Failure("stock.lot_required", "Debe indicar el número de lote.");

            if (ctx.ExpirationDate is null)
                return Result<object?>.Failure("stock.expiration_required", "Debe indicar la fecha de vencimiento del lote.");
        }

        return Result<object?>.Ok(null);
    }

    public async Task<Result<object?>> ApplySaleAsync(StockApplyContext ctx, CancellationToken cancellationToken = default)
    {
        var explicitLot = ctx.StockLotId is { } id && id != Guid.Empty;
        if (explicitLot)
            return await ApplySaleExplicitLotAsync(ctx, cancellationToken);

        return await ApplySaleFefoSplitAsync(ctx, cancellationToken);
    }

    /// <summary>Override explícito: un solo lote; no reparte a otros.</summary>
    private async Task<Result<object?>> ApplySaleExplicitLotAsync(
        StockApplyContext ctx,
        CancellationToken cancellationToken)
    {
        var lot = await LoadLotAsync(ctx.Product.Id, ctx.StockLotId, cancellationToken);
        if (lot is null)
            return Result<object?>.Failure("stock.lot_not_found", "El lote no existe o no pertenece al producto.");

        if (lot.ExpirationDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<object?>.Failure("stock.lot_expired", $"El lote '{lot.LotNumber}' está vencido.");

        if (lot.Quantity < ctx.Quantity)
        {
            return Result<object?>.Failure(
                "stock.insufficient",
                $"Stock insuficiente en lote '{lot.LotNumber}'. Disponible: {lot.Quantity}.");
        }

        await DeductLotForSaleAsync(ctx, lot, ctx.Quantity, cancellationToken);
        return Result<object?>.Ok(null);
    }

    /// <summary>
    /// FEFO: reparte la cantidad entre lotes no vencidos en orden de vencimiento
    /// hasta satisfacer la línea; falla si el total FEFO no alcanza.
    /// </summary>
    private async Task<Result<object?>> ApplySaleFefoSplitAsync(
        StockApplyContext ctx,
        CancellationToken cancellationToken)
    {
        var lots = await LoadFefoLotsAsync(ctx.Product.Id, cancellationToken);
        if (lots.Count == 0)
        {
            return Result<object?>.Failure(
                "stock.no_fefo_lot",
                "No hay lotes no vencidos con stock para este producto.");
        }

        var available = lots.Sum(l => l.Quantity);
        if (available < ctx.Quantity)
        {
            return Result<object?>.Failure(
                "stock.insufficient",
                $"Stock insuficiente en lotes FEFO. Disponible: {available}.");
        }

        var remaining = ctx.Quantity;
        foreach (var lot in lots)
        {
            if (remaining <= 0m)
                break;

            var take = Math.Min(lot.Quantity, remaining);
            if (take <= 0m)
                continue;

            await DeductLotForSaleAsync(ctx, lot, take, cancellationToken);
            remaining -= take;
        }

        if (remaining > 0m)
        {
            return Result<object?>.Failure(
                "stock.insufficient",
                "Stock insuficiente en lotes FEFO.");
        }

        return Result<object?>.Ok(null);
    }

    private async Task DeductLotForSaleAsync(
        StockApplyContext ctx,
        StockLot lot,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        lot.Quantity -= quantity;
        await SyncProductStockAsync(ctx.Product, cancellationToken);

        ctx.AppliedAllocations.Add(
            new StockLotAllocation(lot.Id, quantity, lot.LotNumber, lot.ExpirationDate));

        if (ctx.AppliedStockLotId is null)
        {
            ctx.AppliedStockLotId = lot.Id;
            ctx.AppliedLotNumber = lot.LotNumber;
            ctx.AppliedExpiration = lot.ExpirationDate;
        }

        StockMovementWriter.AddMovement(
            _db,
            ctx.Product,
            StockMovementType.Sale,
            -quantity,
            lot.Id,
            lot.LotNumber,
            lot.ExpirationDate,
            ctx.ReasonCode,
            ctx.ReasonNote,
            ctx.ReferenceId,
            ctx.CreatedByUserId);
    }

    public Task<Result<object?>> ApplyPurchaseAsync(StockApplyContext ctx, CancellationToken cancellationToken = default)
        => UpsertLotInAsync(ctx, StockMovementType.Purchase, cancellationToken);

    public async Task<Result<object?>> ApplyAdjustmentAsync(StockApplyContext ctx, CancellationToken cancellationToken = default)
    {
        if (ctx.Quantity > 0m)
            return await UpsertLotInAsync(ctx, StockMovementType.Adjustment, cancellationToken);

        var lot = await LoadLotAsync(ctx.Product.Id, ctx.StockLotId, cancellationToken);
        if (lot is null)
            return Result<object?>.Failure("stock.lot_not_found", "El lote no existe o no pertenece al producto.");

        // Egreso por ajuste (p. ej. ExpiredDisposal) sí puede descontar lotes vencidos; la venta no.
        var qty = Math.Abs(ctx.Quantity);
        if (lot.Quantity < qty)
        {
            return Result<object?>.Failure(
                "stock.insufficient",
                $"Stock insuficiente en lote '{lot.LotNumber}'. Disponible: {lot.Quantity}.");
        }

        lot.Quantity -= qty;
        await SyncProductStockAsync(ctx.Product, cancellationToken);
        ctx.AppliedStockLotId = lot.Id;
        ctx.AppliedLotNumber = lot.LotNumber;
        ctx.AppliedExpiration = lot.ExpirationDate;

        StockMovementWriter.AddMovement(
            _db,
            ctx.Product,
            StockMovementType.Adjustment,
            -qty,
            lot.Id,
            lot.LotNumber,
            lot.ExpirationDate,
            ctx.ReasonCode,
            ctx.ReasonNote,
            ctx.ReferenceId,
            ctx.CreatedByUserId);

        return Result<object?>.Ok(null);
    }

    public async Task<Result<object?>> ApplyProductSeedAsync(StockApplyContext ctx, CancellationToken cancellationToken = default)
    {
        if (ctx.Quantity == 0m)
            return Result<object?>.Ok(null);

        var seedCtx = new StockApplyContext
        {
            Product = ctx.Product,
            Quantity = ctx.Quantity,
            LotNumber = string.IsNullOrWhiteSpace(ctx.LotNumber) ? DefaultLotNumber : ctx.LotNumber.Trim(),
            ExpirationDate = ctx.ExpirationDate ?? DefaultExpiration,
            ReasonNote = string.IsNullOrWhiteSpace(ctx.ReasonNote) ? "Stock inicial" : ctx.ReasonNote,
            ReferenceId = ctx.ReferenceId,
            CreatedByUserId = ctx.CreatedByUserId
        };

        return await UpsertLotInAsync(seedCtx, StockMovementType.ProductSeed, cancellationToken);
    }

    private async Task<Result<object?>> UpsertLotInAsync(
        StockApplyContext ctx,
        StockMovementType type,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ctx.LotNumber) || ctx.ExpirationDate is null)
        {
            return Result<object?>.Failure(
                "stock.lot_required",
                "Lote y vencimiento son obligatorios.");
        }

        var lotNumber = ctx.LotNumber.Trim();
        var lot = await _db.StockLots
            .FirstOrDefaultAsync(
                l => l.ProductId == ctx.Product.Id && l.LotNumber == lotNumber,
                cancellationToken);

        if (lot is null)
        {
            lot = new StockLot
            {
                Id = Guid.NewGuid(),
                ProductId = ctx.Product.Id,
                LotNumber = lotNumber,
                ExpirationDate = ctx.ExpirationDate.Value,
                Quantity = 0m,
                CreatedAt = DateTime.UtcNow
            };
            _db.StockLots.Add(lot);
        }
        else if (lot.Quantity > 0m && lot.ExpirationDate != ctx.ExpirationDate.Value)
        {
            return Result<object?>.Failure(
                "stock.lot_expiration_mismatch",
                $"El lote '{lotNumber}' ya existe con vencimiento {lot.ExpirationDate:yyyy-MM-dd}.");
        }
        else
        {
            lot.ExpirationDate = ctx.ExpirationDate.Value;
        }

        lot.Quantity += ctx.Quantity;
        await SyncProductStockAsync(ctx.Product, cancellationToken);

        ctx.AppliedStockLotId = lot.Id;
        ctx.AppliedLotNumber = lot.LotNumber;
        ctx.AppliedExpiration = lot.ExpirationDate;

        StockMovementWriter.AddMovement(
            _db,
            ctx.Product,
            type,
            ctx.Quantity,
            lot.Id,
            lot.LotNumber,
            lot.ExpirationDate,
            ctx.ReasonCode,
            ctx.ReasonNote,
            ctx.ReferenceId,
            ctx.CreatedByUserId);

        return Result<object?>.Ok(null);
    }

    private async Task<StockLot?> LoadLotAsync(
        Guid productId,
        Guid? stockLotId,
        CancellationToken cancellationToken)
    {
        if (stockLotId is null || stockLotId == Guid.Empty)
            return null;

        return await _db.StockLots
            .FirstOrDefaultAsync(l => l.Id == stockLotId && l.ProductId == productId, cancellationToken);
    }

    /// <summary>
    /// FEFO: lotes no vencidos con cantidad &gt; 0, ordenados por vencimiento y número de lote.
    /// </summary>
    private async Task<List<StockLot>> LoadFefoLotsAsync(Guid productId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _db.StockLots
            .Where(l => l.ProductId == productId && l.ExpirationDate >= today && l.Quantity > 0m)
            .OrderBy(l => l.ExpirationDate)
            .ThenBy(l => l.LotNumber)
            .ToListAsync(cancellationToken);
    }

    private async Task SyncProductStockAsync(Product product, CancellationToken cancellationToken)
    {
        var byId = _db.ChangeTracker.Entries<StockLot>()
            .Where(e => e.Entity.ProductId == product.Id && e.State != EntityState.Deleted)
            .Select(e => e.Entity)
            .GroupBy(e => e.Id)
            .Select(g => g.First())
            .ToDictionary(l => l.Id);

        var fromDb = await _db.StockLots
            .Where(l => l.ProductId == product.Id)
            .ToListAsync(cancellationToken);

        foreach (var lot in fromDb)
        {
            if (!byId.ContainsKey(lot.Id))
                byId[lot.Id] = lot;
        }

        product.Stock = byId.Values.Sum(l => l.Quantity);
    }
}
