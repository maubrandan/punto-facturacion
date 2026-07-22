using POS.Domain.Common;

namespace POS.Domain.Entities;

/// <summary>Lote de inventario (principalmente Farmacia). Cantidad en unidades enteras vía policy.</summary>
public sealed class StockLot : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    public Product? Product { get; set; }

    public string LotNumber { get; set; } = string.Empty;

    public DateOnly ExpirationDate { get; set; }

    public decimal Quantity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
