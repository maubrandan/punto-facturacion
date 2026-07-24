using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class SaleReturnLine : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public Guid SaleReturnId { get; set; }

    public SaleReturn? SaleReturn { get; set; }

    public Guid SaleDetailId { get; set; }

    public SaleDetail? SaleDetail { get; set; }

    public Guid ProductId { get; set; }

    public Product? Product { get; set; }

    public decimal Quantity { get; set; }

    public Guid? StockLotId { get; set; }

    public decimal LineNetSubtotal { get; set; }

    public decimal LineTaxAmount { get; set; }

    public decimal UnitNetPrice { get; set; }

    public decimal TaxRate { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string ProductExtendedDataJson { get; set; } = "{}";
}
