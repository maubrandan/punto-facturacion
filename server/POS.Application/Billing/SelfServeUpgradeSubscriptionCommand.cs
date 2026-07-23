using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Domain.Entities;

namespace POS.Application.Billing;

public sealed record SelfServeUpgradeSubscriptionCommand(
    string PlanCode,
    BillingCycle BillingCycle,
    string? SuccessUrl,
    string? CancelUrl);

public sealed class SelfServeUpgradeResultDto
{
    public required TenantSubscriptionDto Subscription { get; init; }

    public SubscriptionInvoiceDto? Invoice { get; init; }

    public bool AppliedImmediately { get; init; }

    public string? CheckoutUrl { get; init; }

    public string Message { get; init; } = string.Empty;
}

public interface ISelfServeUpgradeSubscriptionHandler
{
    Task<Result<SelfServeUpgradeResultDto>> HandleAsync(
        SelfServeUpgradeSubscriptionCommand command,
        CancellationToken cancellationToken = default);
}
