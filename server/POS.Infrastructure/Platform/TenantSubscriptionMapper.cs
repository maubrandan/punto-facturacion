using POS.Application.Contracts.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;

namespace POS.Infrastructure.Platform;

internal static class TenantSubscriptionMapper
{
    public static TenantSubscriptionDto ToDto(TenantSubscription row, string? matchedPlanCode) =>
        new()
        {
            TenantId = row.TenantId,
            PlanCode = row.PlanCode,
            MatchedPlanCode = matchedPlanCode,
            EntitlementsMatchPlan = matchedPlanCode is not null
                && string.Equals(matchedPlanCode, row.PlanCode, StringComparison.OrdinalIgnoreCase),
            Status = row.Status,
            BillingCycle = row.BillingCycle,
            Provider = row.Provider,
            ExternalCustomerId = row.ExternalCustomerId,
            ExternalSubscriptionId = row.ExternalSubscriptionId,
            CurrentPeriodStartUtc = row.CurrentPeriodStartUtc,
            CurrentPeriodEndUtc = row.CurrentPeriodEndUtc,
            TrialEndsAtUtc = row.TrialEndsAtUtc,
            CancelAtPeriodEnd = row.CancelAtPeriodEnd,
            CanceledAtUtc = row.CanceledAtUtc,
            PastDueSinceUtc = row.PastDueSinceUtc,
            DunningAttemptCount = row.DunningAttemptCount,
            GracePeriodEndsAtUtc = row.GracePeriodEndsAtUtc,
            Notes = row.Notes,
            UpdatedAtUtc = row.UpdatedAtUtc
        };

    public static TenantSubscription CreateDefault(
        string tenantId,
        string planCode,
        DateTime now,
        BillingCycle cycle = BillingCycle.Monthly) =>
        new()
        {
            TenantId = tenantId,
            PlanCode = TenantPlanPresets.Normalize(planCode),
            Status = SubscriptionStatus.Active,
            BillingCycle = cycle,
            Provider = BillingProvider.Manual,
            CurrentPeriodStartUtc = now,
            CurrentPeriodEndUtc = AddPeriod(now, cycle),
            CancelAtPeriodEnd = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    public static DateTime AddPeriod(DateTime startUtc, BillingCycle cycle) =>
        SubscriptionLifecycleRules.AddPeriod(startUtc, cycle);

    public static string InferPlanFromEntitlements(TenantEntitlement? entitlements)
    {
        var caps = TenantEntitlementsMapper.FromRow(entitlements);
        return caps.MatchedPlanCode ?? TenantPlanPresets.Unlimited;
    }
}
