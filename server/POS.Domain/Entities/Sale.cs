using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class Sale : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    /// <summary>Fecha/hora de la operación (UTC) para reportes e índices.</summary>
    public DateTime Date { get; set; } = DateTime.UtcNow;

    public decimal TotalNet { get; set; }

    public decimal TotalTax { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedByUserId { get; set; }

    /// <summary>Snapshot del nombre de cajero al registrar la venta (evita JOIN en listados).</summary>
    public string? CreatedByUserName { get; set; }

    public Guid? CashSessionId { get; set; }

    public CashSession? CashSession { get; set; }

    public ICollection<SaleDetail> Details { get; set; } = new List<SaleDetail>();

    public ICollection<SalePayment> Payments { get; set; } = new List<SalePayment>();

    public ICollection<FiscalDocument> FiscalDocuments { get; set; } = new List<FiscalDocument>();
}
