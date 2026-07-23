using POS.Domain.Entities;

namespace POS.Application.Platform;

public sealed record UpdateTenantSubscriptionCommand(
    string TenantId,
    string PlanCode,
    SubscriptionStatus Status,
    BillingCycle BillingCycle,
    DateTime? CurrentPeriodStartUtc,
    DateTime? CurrentPeriodEndUtc,
    DateTime? TrialEndsAtUtc,
    bool CancelAtPeriodEnd,
    string? Notes,
    string Justification);
