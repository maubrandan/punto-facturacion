using POS.Application.Common;
using POS.Application.Inventory;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Inventory;

public sealed class HardwareStockPolicy : IStockPolicy
{
    private readonly ApplicationDbContext _db;

    public HardwareStockPolicy(ApplicationDbContext db) => _db = db;

    public string BusinessType => BusinessTypeNames.Ferreteria;

    public Result<object?> ValidateQuantity(decimal quantity) =>
        StockQuantityRules.RequireFractionalAllowed(quantity);

    public Result<object?> ValidateSaleLine(StockLineContext line) =>
        ValidateQuantity(line.Quantity);

    public Result<object?> ValidatePurchaseLine(StockLineContext line) =>
        ValidateQuantity(line.Quantity);

    public Result<object?> ValidateAdjustment(StockAdjustContext ctx)
    {
        if (ctx.QuantityDelta == 0m)
            return Result<object?>.Failure("stock.adjustment", "El ajuste no puede ser cero.");

        var absCheck = StockQuantityRules.RequireFractionalAllowed(Math.Abs(ctx.QuantityDelta));
        if (!absCheck.IsSuccess)
            return absCheck;

        var reason = StockQuantityRules.RequireKnownAdjustmentReason(ctx.ReasonCode);
        if (!reason.IsSuccess)
            return reason;

        return Result<object?>.Ok(null);
    }

    public Task<Result<object?>> ApplySaleAsync(StockApplyContext ctx, CancellationToken cancellationToken = default)
        => Task.FromResult(ApplySimpleOut(ctx, StockMovementType.Sale));

    public Task<Result<object?>> ApplyPurchaseAsync(StockApplyContext ctx, CancellationToken cancellationToken = default)
        => Task.FromResult(ApplySimpleIn(ctx, StockMovementType.Purchase));

    public Task<Result<object?>> ApplyAdjustmentAsync(StockApplyContext ctx, CancellationToken cancellationToken = default)
    {
        var delta = ctx.Quantity;
        if (delta > 0m)
            return Task.FromResult(ApplySimpleIn(ctx, StockMovementType.Adjustment, delta));

        var outCtx = new StockApplyContext
        {
            Product = ctx.Product,
            Quantity = Math.Abs(delta),
            ReasonCode = ctx.ReasonCode,
            ReasonNote = ctx.ReasonNote,
            ReferenceId = ctx.ReferenceId,
            CreatedByUserId = ctx.CreatedByUserId
        };
        return Task.FromResult(ApplySimpleOut(outCtx, StockMovementType.Adjustment));
    }

    public Task<Result<object?>> ApplyProductSeedAsync(StockApplyContext ctx, CancellationToken cancellationToken = default)
    {
        if (ctx.Quantity == 0m)
            return Task.FromResult(Result<object?>.Ok(null));

        return Task.FromResult(ApplySimpleIn(ctx, StockMovementType.ProductSeed));
    }

    private Result<object?> ApplySimpleIn(StockApplyContext ctx, StockMovementType type, decimal? overrideQty = null)
    {
        var qty = overrideQty ?? ctx.Quantity;
        ctx.Product.Stock += qty;
        StockMovementWriter.AddMovement(
            _db,
            ctx.Product,
            type,
            qty,
            null,
            null,
            null,
            ctx.ReasonCode,
            ctx.ReasonNote,
            ctx.ReferenceId,
            ctx.CreatedByUserId);
        return Result<object?>.Ok(null);
    }

    private Result<object?> ApplySimpleOut(StockApplyContext ctx, StockMovementType type)
    {
        if (ctx.Product.Stock < ctx.Quantity)
        {
            return Result<object?>.Failure(
                "stock.insufficient",
                $"Stock insuficiente para '{ctx.Product.Name}'. Disponible: {ctx.Product.Stock}.");
        }

        ctx.Product.Stock -= ctx.Quantity;
        StockMovementWriter.AddMovement(
            _db,
            ctx.Product,
            type,
            -ctx.Quantity,
            null,
            null,
            null,
            ctx.ReasonCode,
            ctx.ReasonNote,
            ctx.ReferenceId,
            ctx.CreatedByUserId);
        return Result<object?>.Ok(null);
    }
}
