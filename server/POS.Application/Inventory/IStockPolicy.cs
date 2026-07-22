using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Inventory;

public sealed record StockLineContext(
    Guid ProductId,
    decimal Quantity,
    Guid? StockLotId = null,
    string? LotNumber = null,
    DateOnly? ExpirationDate = null);

public sealed record StockAdjustContext(
    Guid ProductId,
    decimal QuantityDelta,
    string Reason,
    Guid? StockLotId = null,
    string? LotNumber = null,
    DateOnly? ExpirationDate = null);

/// <summary>
/// Contexto de aplicación de stock. El caller carga <see cref="Product"/> tracked;
/// la policy muta stock/lotes y agrega movimientos al DbContext vía infraestructura.
/// </summary>
public sealed class StockApplyContext
{
    public required Product Product { get; init; }

    public required decimal Quantity { get; init; }

    public Guid? StockLotId { get; init; }

    public string? LotNumber { get; init; }

    public DateOnly? ExpirationDate { get; init; }

    public string? Reason { get; init; }

    public Guid? ReferenceId { get; init; }

    public string CreatedByUserId { get; init; } = string.Empty;

    /// <summary>Salida: lote afectado (venta/compra farmacia).</summary>
    public Guid? AppliedStockLotId { get; set; }

    public string? AppliedLotNumber { get; set; }

    public DateOnly? AppliedExpiration { get; set; }
}

public interface IStockPolicy
{
    string BusinessType { get; }

    Result<object?> ValidateQuantity(decimal quantity);

    Result<object?> ValidateSaleLine(StockLineContext line);

    Result<object?> ValidatePurchaseLine(StockLineContext line);

    Result<object?> ValidateAdjustment(StockAdjustContext ctx);

    Task<Result<object?>> ApplySaleAsync(StockApplyContext ctx, CancellationToken cancellationToken = default);

    Task<Result<object?>> ApplyPurchaseAsync(StockApplyContext ctx, CancellationToken cancellationToken = default);

    Task<Result<object?>> ApplyAdjustmentAsync(StockApplyContext ctx, CancellationToken cancellationToken = default);

    Task<Result<object?>> ApplyProductSeedAsync(StockApplyContext ctx, CancellationToken cancellationToken = default);
}

public interface IStockPolicyFactory
{
    Task<IStockPolicy> ForCurrentTenantAsync(CancellationToken cancellationToken = default);

    Task<IStockPolicy> ForBusinessTypeAsync(string businessType, CancellationToken cancellationToken = default);
}
