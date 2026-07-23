namespace POS.Domain.Entities;

/// <summary>Estado de factura SaaS (suscripción), no de factura fiscal POS.</summary>
public enum SubscriptionInvoiceStatus
{
    Draft = 0,

    Open = 1,

    Paid = 2,

    Void = 3,

    Uncollectible = 4
}
