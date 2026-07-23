using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Interfaces.Billing;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Infrastructure.Configuration;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Billing;

public sealed class SubscriptionBillingJobs : ISubscriptionBillingJobs
{
    private readonly ApplicationDbContext _db;
    private readonly ISubscriptionInvoiceFactory _invoices;
    private readonly IPlatformAuditService _audit;
    private readonly BillingOptions _options;
    private readonly ILogger<SubscriptionBillingJobs> _logger;

    public SubscriptionBillingJobs(
        ApplicationDbContext db,
        ISubscriptionInvoiceFactory invoices,
        IPlatformAuditService audit,
        IOptions<BillingOptions> options,
        ILogger<SubscriptionBillingJobs> logger)
    {
        _db = db;
        _invoices = invoices;
        _audit = audit;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> ProcessRenewalsAsync(CancellationToken cancellationToken = default)
    {
        if (_options.IsNone)
            return 0;

        var now = DateTime.UtcNow;
        var due = await _db.TenantSubscriptions
            .Where(s =>
                s.Status != SubscriptionStatus.Canceled
                && s.CurrentPeriodEndUtc <= now)
            .OrderBy(s => s.CurrentPeriodEndUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var sub in due)
        {
            if (SubscriptionLifecycleRules.ShouldCancelAtPeriodEnd(sub, now))
            {
                sub.MarkCanceled(now);
                await _audit.LogAsync(
                    new PlatformAuditEventData(
                        Action: "TenantSubscriptionCanceledAtPeriodEnd",
                        ResourceType: nameof(TenantSubscription),
                        ResourceId: sub.TenantId,
                        Details: $"periodEnd={sub.CurrentPeriodEndUtc:O}",
                        AffectedTenantId: sub.TenantId),
                    cancellationToken);
                processed++;
                continue;
            }

            if (sub.Status == SubscriptionStatus.PastDue)
                continue;

            if (ShouldDeferRenewalToGateway(sub))
            {
                if (SubscriptionLifecycleRules.ShouldEnterPastDue(sub, now))
                {
                    sub.MarkPastDue(now, TimeSpan.FromDays(_options.GracePeriodDays));
                    await _audit.LogAsync(
                        new PlatformAuditEventData(
                            Action: "TenantSubscriptionPastDue",
                            ResourceType: nameof(TenantSubscription),
                            ResourceId: sub.TenantId,
                            Details: $"graceEnds={sub.GracePeriodEndsAtUtc:O}; reason=period_expired_awaiting_gateway",
                            AffectedTenantId: sub.TenantId),
                        cancellationToken);
                    processed++;
                }

                continue;
            }

            var start = sub.CurrentPeriodEndUtc;
            var end = SubscriptionLifecycleRules.AddPeriod(start, sub.BillingCycle);
            sub.ApplyRenewal(start, end, now);

            await _invoices.CreateForPeriodAsync(
                sub,
                start,
                end,
                SubscriptionInvoiceStatus.Paid,
                now,
                "Renovación automática de período",
                cancellationToken);

            await _audit.LogAsync(
                new PlatformAuditEventData(
                    Action: "TenantSubscriptionRenewed",
                    ResourceType: nameof(TenantSubscription),
                    ResourceId: sub.TenantId,
                    Details: $"period={start:O}/{end:O}; plan={sub.PlanCode}",
                    AffectedTenantId: sub.TenantId),
                cancellationToken);

            processed++;
        }

        if (processed > 0)
            await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Billing renewal job processed {Count} subscriptions.", processed);
        return processed;
    }

    public async Task<int> ProcessDunningAsync(CancellationToken cancellationToken = default)
    {
        if (_options.IsNone)
            return 0;

        var now = DateTime.UtcNow;
        var interval = TimeSpan.FromHours(_options.DunningIntervalHours);
        var processed = 0;

        var pastDue = await _db.TenantSubscriptions
            .Where(s => s.Status == SubscriptionStatus.PastDue)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var sub in pastDue)
        {
            if (SubscriptionLifecycleRules.ShouldCancelAfterGrace(sub, now))
            {
                sub.MarkCanceled(now);
                await _audit.LogAsync(
                    new PlatformAuditEventData(
                        Action: "TenantSubscriptionCanceledAfterGrace",
                        ResourceType: nameof(TenantSubscription),
                        ResourceId: sub.TenantId,
                        Details: $"pastDueSince={sub.PastDueSinceUtc:O}; attempts={sub.DunningAttemptCount}",
                        AffectedTenantId: sub.TenantId),
                    cancellationToken);
                processed++;
                continue;
            }

            if (SubscriptionLifecycleRules.NeedsDunningAttempt(
                    sub,
                    now,
                    interval,
                    _options.MaxDunningAttempts))
            {
                sub.RecordDunningAttempt(now);
                await _audit.LogAsync(
                    new PlatformAuditEventData(
                        Action: "TenantSubscriptionDunningAttempt",
                        ResourceType: nameof(TenantSubscription),
                        ResourceId: sub.TenantId,
                        Details: $"attempt={sub.DunningAttemptCount}; last={now:O}",
                        AffectedTenantId: sub.TenantId),
                    cancellationToken);
                processed++;
            }
        }

        if (processed > 0)
            await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Billing dunning job processed {Count} actions.", processed);
        return processed;
    }

    /// <summary>
    /// Gateways con keys reales no auto-renuevan: esperan cobro/webhook y marcan PastDue.
    /// Manual y simulación renuevan en proceso.
    /// </summary>
    private bool ShouldDeferRenewalToGateway(TenantSubscription sub)
    {
        if (_options.IsManual || sub.Provider == BillingProvider.Manual)
            return false;

        if (_options.IsStripe && _options.Stripe.HasSecretKey)
            return true;
        if (_options.IsMercadoPago && _options.MercadoPago.HasAccessToken)
            return true;

        return false;
    }
}
