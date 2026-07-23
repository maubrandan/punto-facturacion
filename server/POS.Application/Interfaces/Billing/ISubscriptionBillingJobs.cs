using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Interfaces.Billing;

/// <summary>
/// Orquestación de renovación / dunning (usada por hosted services y tests).
/// </summary>
public interface ISubscriptionBillingJobs
{
    Task<int> ProcessRenewalsAsync(CancellationToken cancellationToken = default);

    Task<int> ProcessDunningAsync(CancellationToken cancellationToken = default);
}

/// <summary>Creación de facturas SaaS (uso interno Application/Infrastructure).</summary>
public interface ISubscriptionInvoiceFactory
{
    Task<SubscriptionInvoice> CreateForPeriodAsync(
        TenantSubscription subscription,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        SubscriptionInvoiceStatus status,
        DateTime nowUtc,
        string? notes,
        CancellationToken cancellationToken = default);
}
