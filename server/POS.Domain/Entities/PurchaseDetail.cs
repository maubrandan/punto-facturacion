using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class PurchaseDetail : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public Guid PurchaseId { get; set; }

    public Purchase? Purchase { get; set; }

    public Guid ProductId { get; set; }

    public Product? Product { get; set; }

    public int Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal Subtotal { get; set; }

    /// <summary>Snapshot de nombre e identificación al registrar la compra (historial inmutable).</summary>
    public string ProductName { get; set; } = string.Empty;

    public string ProductSku { get; set; } = string.Empty;
}
