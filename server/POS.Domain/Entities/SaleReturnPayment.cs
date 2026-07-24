using POS.Domain.Common;

namespace POS.Domain.Entities;

/// <summary>Reverso de cobro por el mismo medio que la venta original.</summary>
public sealed class SaleReturnPayment : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public Guid SaleReturnId { get; set; }

    public SaleReturn? SaleReturn { get; set; }

    public PaymentMethod Method { get; set; }

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
