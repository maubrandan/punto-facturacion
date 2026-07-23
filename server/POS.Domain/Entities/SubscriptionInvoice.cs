using POS.Domain.Common;

namespace POS.Domain.Entities;

/// <summary>
/// Factura / recibo SaaS de suscripción (no confundir con <see cref="FiscalDocument"/> de ventas POS).
/// </summary>
public sealed class SubscriptionInvoice : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    /// <summary>Número legible único por tenant (ej. INV-202607-0001).</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    public SubscriptionInvoiceStatus Status { get; set; } = SubscriptionInvoiceStatus.Open;

    public string PlanCode { get; set; } = string.Empty;

    public BillingCycle BillingCycle { get; set; }

    public DateTime PeriodStartUtc { get; set; }

    public DateTime PeriodEndUtc { get; set; }

    public decimal Amount { get; set; }

    /// <summary>ISO 4217 (ARS por defecto en LatAm).</summary>
    public string Currency { get; set; } = "ARS";

    public BillingProvider Provider { get; set; }

    public string? ExternalInvoiceId { get; set; }

    public DateTime DueAtUtc { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public void MarkPaid(DateTime paidAtUtc)
    {
        Status = SubscriptionInvoiceStatus.Paid;
        PaidAtUtc = paidAtUtc;
    }

    public void MarkUncollectible()
    {
        Status = SubscriptionInvoiceStatus.Uncollectible;
    }

    public void Void()
    {
        Status = SubscriptionInvoiceStatus.Void;
    }
}
