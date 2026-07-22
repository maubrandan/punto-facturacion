using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class SaleDetail : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public Guid SaleId { get; set; }

    public Sale? Sale { get; set; }

    public Guid ProductId { get; set; }

    public Product? Product { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>Lote debitado (Farmacia). Null en otros rubros.</summary>
    public Guid? StockLotId { get; set; }

    /// <summary>Subtotal neto de la línea (NetPrice × cantidad) al momento de la venta.</summary>
    public decimal LineNetSubtotal { get; set; }

    public decimal LineTaxAmount { get; set; }

    public decimal UnitNetPrice { get; set; }

    public decimal TaxRate { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string ProductExtendedDataJson { get; set; } = "{}";
}
