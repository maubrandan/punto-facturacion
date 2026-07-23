using Microsoft.EntityFrameworkCore;
using POS.Application.Billing;
using POS.Application.Interfaces.Billing;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Billing;

public sealed class SubscriptionInvoiceFactory : ISubscriptionInvoiceFactory
{
    private readonly ApplicationDbContext _db;

    public SubscriptionInvoiceFactory(ApplicationDbContext db) => _db = db;

    public async Task<SubscriptionInvoice> CreateForPeriodAsync(
        TenantSubscription subscription,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        SubscriptionInvoiceStatus status,
        DateTime nowUtc,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var yearMonth = nowUtc.ToString("yyyyMM");
        var prefix = $"INV-{yearMonth}-";
        var count = await _db.SubscriptionInvoices
            .IgnoreQueryFilters()
            .CountAsync(
                i => i.TenantId == subscription.TenantId && i.InvoiceNumber.StartsWith(prefix),
                cancellationToken);

        var invoice = new SubscriptionInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = subscription.TenantId,
            InvoiceNumber = $"{prefix}{(count + 1):D4}",
            Status = status,
            PlanCode = subscription.PlanCode,
            BillingCycle = subscription.BillingCycle,
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            Amount = SaaSPlanPricing.AmountFor(subscription.PlanCode, subscription.BillingCycle),
            Currency = SaaSPlanPricing.DefaultCurrency,
            Provider = subscription.Provider,
            DueAtUtc = periodStartUtc,
            PaidAtUtc = status == SubscriptionInvoiceStatus.Paid ? nowUtc : null,
            Notes = notes,
            CreatedAtUtc = nowUtc
        };

        _db.SubscriptionInvoices.Add(invoice);
        return invoice;
    }
}
