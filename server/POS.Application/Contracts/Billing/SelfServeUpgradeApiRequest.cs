using POS.Domain.Entities;

namespace POS.Application.Contracts.Billing;

public sealed class SelfServeUpgradeApiRequest
{
    public string PlanCode { get; set; } = string.Empty;

    public BillingCycle BillingCycle { get; set; }

    public string? SuccessUrl { get; set; }

    public string? CancelUrl { get; set; }
}
