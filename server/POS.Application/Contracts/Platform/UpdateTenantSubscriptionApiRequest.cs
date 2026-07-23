using POS.Domain.Entities;

namespace POS.Application.Contracts.Platform;

public sealed class UpdateTenantSubscriptionApiRequest
{
    public required string PlanCode { get; init; }

    public SubscriptionStatus Status { get; init; } = SubscriptionStatus.Active;

    public BillingCycle BillingCycle { get; init; } = BillingCycle.Monthly;

    public DateTime? CurrentPeriodStartUtc { get; init; }

    public DateTime? CurrentPeriodEndUtc { get; init; }

    public DateTime? TrialEndsAtUtc { get; init; }

    public bool CancelAtPeriodEnd { get; init; }

    public string? Notes { get; init; }

    public required string Justification { get; init; }
}
