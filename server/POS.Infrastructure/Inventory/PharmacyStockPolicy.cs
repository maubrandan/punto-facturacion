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
        var qty = ValidateQuantity(line.Quantity);
        if (!qty.IsSuccess)
            return qty;

        if (line.StockLotId is null || line.StockLotId == Guid.Empty)
        {
            return Result<object?>.Failure(
                "stock.lot_required",
                "Debe indicar el lote a vender (Farmacia).");
        }

        return Result<object?>.Ok(null);
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

        if (string.IsNullOrWhiteSpace(ctx.Reason))
            return Result<object?>.Failure("stock.adjustment", "El motivo del ajuste es obligatorio.");

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

        lot.Quantity -= ctx.Quantity;
        await SyncProductStockAsync(ctx.Product, cancellationToken);
        ctx.AppliedStockLotId = lot.Id;
        ctx.AppliedLotNumber = lot.LotNumber;
        ctx.AppliedExpiration = lot.ExpirationDate;

        StockMovementWriter.AddMovement(
            _db,
            ctx.Product,
            StockMovementType.Sale,
            -ctx.Quantity,
            lot.Id,
            lot.LotNumber,
            lot.ExpirationDate,
            ctx.Reason,
            ctx.ReferenceId,
            ctx.CreatedByUserId);

        return Result<object?>.Ok(null);
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

        if (lot.ExpirationDate < DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<object?>.Failure("stock.lot_expired", $"El lote '{lot.LotNumber}' está vencido.");

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
            ctx.Reason,
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
            Reason = ctx.Reason ?? "Stock inicial",
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
            ctx.Reason,
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
