using POS.Domain.Common;

namespace POS.Domain.Entities;

/// <summary>Movimiento de kardex por producto (y lote opcional).</summary>
public sealed class StockMovement : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    public Product? Product { get; set; }

    public StockMovementType Type { get; set; }

    /// <summary>Delta: positivo ingreso, negativo egreso.</summary>
    public decimal QuantityDelta { get; set; }

    public decimal QuantityAfter { get; set; }

    public Guid? StockLotId { get; set; }

    public StockLot? StockLot { get; set; }

    public string? LotNumberSnapshot { get; set; }

    public DateOnly? ExpirationSnapshot { get; set; }

    /// <summary>
    /// Código tipado del motivo (ajustes). Nulo en venta/compra/seed.
    /// Ver <see cref="StockAdjustmentReasonCodes"/>.
    /// </summary>
    public string? ReasonCode { get; set; }

    /// <summary>Nota libre opcional (ajustes) o texto legado/seed.</summary>
    public string? ReasonNote { get; set; }

    public Guid? ReferenceId { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
