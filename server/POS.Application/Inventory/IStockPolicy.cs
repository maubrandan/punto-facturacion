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
    string ReasonCode,
    string? Note = null,
    Guid? StockLotId = null,
    string? LotNumber = null,
    DateOnly? ExpirationDate = null);

/// <summary>
/// Asignación parcial de cantidad a un lote (p. ej. FEFO multi-lote en Farmacia).
/// </summary>
public sealed record StockLotAllocation(
    Guid StockLotId,
    decimal Quantity,
    string LotNumber,
    DateOnly ExpirationDate);

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

    public string? ReasonCode { get; init; }

    public string? ReasonNote { get; init; }

    public Guid? ReferenceId { get; init; }

    public string CreatedByUserId { get; init; } = string.Empty;

    /// <summary>Salida: lote primario (primer FEFO o override explícito).</summary>
    public Guid? AppliedStockLotId { get; set; }

    public string? AppliedLotNumber { get; set; }

    public DateOnly? AppliedExpiration { get; set; }

    /// <summary>
    /// Salida: asignaciones por lote. En venta FEFO farmacia puede haber más de una;
    /// vacío en rubros sin lotes.
    /// </summary>
    public List<StockLotAllocation> AppliedAllocations { get; } = [];
}

public interface IStockPolicy
{
    string BusinessType { get; }

    Result<object?> ValidateQuantity(decimal quantity);

    Result<object?> ValidateSaleLine(StockLineContext line);

    Result<object?> ValidatePurchaseLine(StockLineContext line);

    Result<object?> ValidateAdjustment(StockAdjustContext ctx);

    Task<Result<object?>> ApplySaleAsync(StockApplyContext ctx, CancellationToken cancellationToken = default);

    /// <summary>Reposición de stock por devolución de venta (misma dirección que compra/entrada).</summary>
    Task<Result<object?>> ApplySaleReturnAsync(StockApplyContext ctx, CancellationToken cancellationToken = default);

    Task<Result<object?>> ApplyPurchaseAsync(StockApplyContext ctx, CancellationToken cancellationToken = default);

    Task<Result<object?>> ApplyAdjustmentAsync(StockApplyContext ctx, CancellationToken cancellationToken = default);

    Task<Result<object?>> ApplyProductSeedAsync(StockApplyContext ctx, CancellationToken cancellationToken = default);
}

public interface IStockPolicyFactory
{
    Task<IStockPolicy> ForCurrentTenantAsync(CancellationToken cancellationToken = default);

    Task<IStockPolicy> ForBusinessTypeAsync(string businessType, CancellationToken cancellationToken = default);
}
