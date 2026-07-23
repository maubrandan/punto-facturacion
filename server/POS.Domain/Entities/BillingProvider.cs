namespace POS.Domain.Entities;

/// <summary>
/// Proveedor de cobro SaaS. El valor activo a nivel app se elige con <c>Billing:Provider</c>
/// (incluye <c>None</c> a nivel config, que no se persiste en la fila).
/// </summary>
public enum BillingProvider
{
    Manual = 0,

    Stripe = 1,

    MercadoPago = 2
}
