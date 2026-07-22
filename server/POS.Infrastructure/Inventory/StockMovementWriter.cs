using POS.Application.Common;
using POS.Application.Inventory;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Inventory;

internal static class StockMovementWriter
{
    public static void AddMovement(
        ApplicationDbContext db,
        Product product,
        StockMovementType type,
        decimal quantityDelta,
        Guid? stockLotId,
        string? lotNumberSnapshot,
        DateOnly? expirationSnapshot,
        string? reason,
        Guid? referenceId,
        string createdByUserId)
    {
        db.StockMovements.Add(
            new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Type = type,
                QuantityDelta = quantityDelta,
                QuantityAfter = product.Stock,
                StockLotId = stockLotId,
                LotNumberSnapshot = lotNumberSnapshot,
                ExpirationSnapshot = expirationSnapshot,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                ReferenceId = referenceId,
                CreatedByUserId = string.IsNullOrWhiteSpace(createdByUserId) ? "system" : createdByUserId,
                CreatedAt = DateTime.UtcNow
            });
    }
}

internal static class StockQuantityRules
{
    public static Result<object?> RequirePositive(decimal quantity)
    {
        if (quantity <= 0m)
            return Result<object?>.Failure("stock.quantity", "La cantidad debe ser mayor a cero.");

        return Result<object?>.Ok(null);
    }

    public static Result<object?> RequireWholePositive(decimal quantity)
    {
        var baseCheck = RequirePositive(quantity);
        if (!baseCheck.IsSuccess)
            return baseCheck;

        if (quantity != decimal.Truncate(quantity))
        {
            return Result<object?>.Failure(
                "stock.quantity_whole",
                "La cantidad debe ser un número entero para este rubro.");
        }

        return Result<object?>.Ok(null);
    }

    public static Result<object?> RequireFractionalAllowed(decimal quantity, int maxScale = 3)
    {
        var baseCheck = RequirePositive(quantity);
        if (!baseCheck.IsSuccess)
            return baseCheck;

        var scaled = decimal.Round(quantity, maxScale, MidpointRounding.AwayFromZero);
        if (scaled != quantity)
        {
            return Result<object?>.Failure(
                "stock.quantity_scale",
                $"La cantidad admite como máximo {maxScale} decimales.");
        }

        return Result<object?>.Ok(null);
    }
}
