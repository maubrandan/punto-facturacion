using POS.Domain.Common;

namespace POS.Domain.Entities;

/// <summary>Devolución comercial de una venta (v1: siempre total).</summary>
public sealed class SaleReturn : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public Guid SaleId { get; set; }

    public Sale? Sale { get; set; }

    public DateTime ReturnedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedByUserId { get; set; }

    public string? CreatedByUserName { get; set; }

    /// <summary>Sesión de caja abierta al momento de la devolución (reembolsos).</summary>
    public Guid? CashSessionId { get; set; }

    public CashSession? CashSession { get; set; }

    public decimal TotalAmount { get; set; }

    /// <summary>NC emitida como parte de esta devolución (si había factura autorizada).</summary>
    public Guid? FiscalDocumentId { get; set; }

    public FiscalDocument? FiscalDocument { get; set; }

    public ICollection<SaleReturnLine> Lines { get; set; } = new List<SaleReturnLine>();

    public ICollection<SaleReturnPayment> Payments { get; set; } = new List<SaleReturnPayment>();
}
