namespace POS.Domain.Entities;

/// <summary>
/// Estado comercial 1:1 por tenant (plan vendido, período, status).
/// Los caps runtime siguen en <see cref="TenantEntitlement"/>; al cambiar plan se re-aplican presets.
/// </summary>
public sealed class TenantSubscription
{
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Código de plan comercial (Starter / Pro / Unlimited).</summary>
    public string PlanCode { get; set; } = string.Empty;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;

    public BillingProvider Provider { get; set; } = BillingProvider.Manual;

    /// <summary>Customer id en Stripe / MercadoPago (si aplica).</summary>
    public string? ExternalCustomerId { get; set; }

    /// <summary>Subscription id en el gateway (si aplica).</summary>
    public string? ExternalSubscriptionId { get; set; }

    public DateTime CurrentPeriodStartUtc { get; set; }

    public DateTime CurrentPeriodEndUtc { get; set; }

    public DateTime? TrialEndsAtUtc { get; set; }

    public bool CancelAtPeriodEnd { get; set; }

    public DateTime? CanceledAtUtc { get; set; }

    /// <summary>Primera vez que entró en PastDue en el ciclo actual de mora.</summary>
    public DateTime? PastDueSinceUtc { get; set; }

    public int DunningAttemptCount { get; set; }

    public DateTime? LastDunningAtUtc { get; set; }

    /// <summary>Fin de gracia tras PastDue; pasado este instante el job puede cancelar.</summary>
    public DateTime? GracePeriodEndsAtUtc { get; set; }

    /// <summary>Notas operativas (no tokens de pago).</summary>
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public void MarkPastDue(DateTime nowUtc, TimeSpan gracePeriod)
    {
        if (Status == SubscriptionStatus.Canceled)
            return;

        Status = SubscriptionStatus.PastDue;
        PastDueSinceUtc ??= nowUtc;
        GracePeriodEndsAtUtc ??= nowUtc.Add(gracePeriod);
        UpdatedAtUtc = nowUtc;
    }

    public void RecordDunningAttempt(DateTime nowUtc)
    {
        DunningAttemptCount++;
        LastDunningAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void ClearDunning(DateTime nowUtc)
    {
        PastDueSinceUtc = null;
        DunningAttemptCount = 0;
        LastDunningAtUtc = null;
        GracePeriodEndsAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkCanceled(DateTime nowUtc)
    {
        Status = SubscriptionStatus.Canceled;
        CanceledAtUtc = nowUtc;
        CancelAtPeriodEnd = false;
        UpdatedAtUtc = nowUtc;
    }

    public void ApplyRenewal(DateTime periodStartUtc, DateTime periodEndUtc, DateTime nowUtc)
    {
        CurrentPeriodStartUtc = periodStartUtc;
        CurrentPeriodEndUtc = periodEndUtc;
        if (Status is SubscriptionStatus.PastDue or SubscriptionStatus.Trialing)
            Status = SubscriptionStatus.Active;
        ClearDunning(nowUtc);
        CanceledAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }
}
