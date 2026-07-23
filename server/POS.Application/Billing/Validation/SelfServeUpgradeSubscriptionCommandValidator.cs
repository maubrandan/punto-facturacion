using FluentValidation;
using POS.Application.Platform;

namespace POS.Application.Billing.Validation;

public sealed class SelfServeUpgradeSubscriptionCommandValidator
    : AbstractValidator<SelfServeUpgradeSubscriptionCommand>
{
    public SelfServeUpgradeSubscriptionCommandValidator()
    {
        RuleFor(x => x.PlanCode)
            .NotEmpty()
            .Must(TenantPlanPresets.IsKnown)
            .WithMessage("planCode debe ser Starter, Pro o Unlimited.");

        RuleFor(x => x.BillingCycle).IsInEnum();
    }
}
