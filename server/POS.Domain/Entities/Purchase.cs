using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class Purchase : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public Guid ProviderId { get; set; }

    public Provider? Provider { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    public decimal Total { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? CashSessionId { get; set; }

    public CashSession? CashSession { get; set; }

    public ICollection<PurchaseDetail> Details { get; set; } = new List<PurchaseDetail>();
}
