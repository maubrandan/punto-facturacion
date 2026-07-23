using POS.Domain.Entities;

namespace POS.Domain.Platform;

/// <summary>Transiciones y reglas de ciclo de vida de suscripción SaaS.</summary>
public static class SubscriptionLifecycleRules
{
    private static readonly Dictionary<string, int> PlanRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Starter"] = 1,
        ["Pro"] = 2,
        ["Unlimited"] = 3
    };

    /// <summary>Cambio de plan (sync entitlements) salvo cancelada sin reactivar en el mismo request.</summary>
    public static bool CanChangePlan(SubscriptionStatus status) =>
        status is SubscriptionStatus.Trialing or SubscriptionStatus.Active or SubscriptionStatus.PastDue;

    public static bool CanCancel(SubscriptionStatus status) =>
        status is SubscriptionStatus.Trialing or SubscriptionStatus.Active or SubscriptionStatus.PastDue;

    public static bool CanSelfServeMutate(SubscriptionStatus status) =>
        status is SubscriptionStatus.Trialing or SubscriptionStatus.Active or SubscriptionStatus.PastDue;

    public static bool IsKnownPlanRank(string planCode) =>
        PlanRank.ContainsKey(planCode.Trim());

    public static int GetPlanRank(string planCode) =>
        PlanRank.TryGetValue(planCode.Trim(), out var rank) ? rank : 0;

    /// <summary>Self-serve solo permite upgrade de plan (mismo plan + cambio de ciclo también ok).</summary>
    public static bool IsSelfServeUpgradeAllowed(string currentPlan, string targetPlan)
    {
        var from = GetPlanRank(currentPlan);
        var to = GetPlanRank(targetPlan);
        return from > 0 && to > 0 && to >= from;
    }

    public static bool IsStrictUpgrade(string currentPlan, string targetPlan) =>
        GetPlanRank(targetPlan) > GetPlanRank(currentPlan);

    public static DateTime AddPeriod(DateTime startUtc, BillingCycle cycle) =>
        cycle == BillingCycle.Yearly ? startUtc.AddYears(1) : startUtc.AddMonths(1);

    /// <summary>
    /// Suscripción activa/trialing cuyo período ya venció y no está marcada cancel-at-end
    /// → candidata a renovación (Manual) o a PastDue (gateways con cobro fallido).
    /// </summary>
    public static bool IsPeriodExpired(TenantSubscription sub, DateTime nowUtc) =>
        sub.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing
        && sub.CurrentPeriodEndUtc <= nowUtc;

    public static bool ShouldCancelAtPeriodEnd(TenantSubscription sub, DateTime nowUtc) =>
        sub.CancelAtPeriodEnd
        && sub.Status != SubscriptionStatus.Canceled
        && sub.CurrentPeriodEndUtc <= nowUtc;

    public static bool ShouldEnterPastDue(TenantSubscription sub, DateTime nowUtc) =>
        IsPeriodExpired(sub, nowUtc)
        && !sub.CancelAtPeriodEnd
        && sub.Status != SubscriptionStatus.PastDue
        && sub.Status != SubscriptionStatus.Canceled;

    public static bool ShouldCancelAfterGrace(TenantSubscription sub, DateTime nowUtc) =>
        sub.Status == SubscriptionStatus.PastDue
        && sub.GracePeriodEndsAtUtc.HasValue
        && sub.GracePeriodEndsAtUtc.Value <= nowUtc;

    public static bool NeedsDunningAttempt(
        TenantSubscription sub,
        DateTime nowUtc,
        TimeSpan minIntervalBetweenAttempts,
        int maxAttempts)
    {
        if (sub.Status != SubscriptionStatus.PastDue)
            return false;
        if (sub.DunningAttemptCount >= maxAttempts)
            return false;
        if (!sub.LastDunningAtUtc.HasValue)
            return true;
        return nowUtc - sub.LastDunningAtUtc.Value >= minIntervalBetweenAttempts;
    }
}
