using POS.Domain.Entities;

namespace POS.Application.Contracts.Platform;

public sealed class TenantSubscriptionDto
{
    public required string TenantId { get; init; }

    public required string PlanCode { get; init; }

    /// <summary>
    /// Preset coincidente con caps actuales de entitlements; null si custom.
    /// Informativo: puede divergir de <see cref="PlanCode"/> si se editaron caps a mano.
    /// </summary>
    public string? MatchedPlanCode { get; init; }

    /// <summary>True si caps de entitlements coinciden exactamente con el plan comercial.</summary>
    public bool EntitlementsMatchPlan { get; init; }

    public SubscriptionStatus Status { get; init; }

    public BillingCycle BillingCycle { get; init; }

    public BillingProvider Provider { get; init; }

    public string? ExternalCustomerId { get; init; }

    public string? ExternalSubscriptionId { get; init; }

    public DateTime CurrentPeriodStartUtc { get; init; }

    public DateTime CurrentPeriodEndUtc { get; init; }

    public DateTime? TrialEndsAtUtc { get; init; }

    public bool CancelAtPeriodEnd { get; init; }

    public DateTime? CanceledAtUtc { get; init; }

    public DateTime? PastDueSinceUtc { get; init; }

    public int DunningAttemptCount { get; init; }

    public DateTime? GracePeriodEndsAtUtc { get; init; }

    public string? Notes { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
