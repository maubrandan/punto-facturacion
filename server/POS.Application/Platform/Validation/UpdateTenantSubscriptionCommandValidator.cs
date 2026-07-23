using FluentValidation;
using POS.Application.Platform;
using POS.Domain.Entities;

namespace POS.Application.Platform.Validation;

public sealed class UpdateTenantSubscriptionCommandValidator : AbstractValidator<UpdateTenantSubscriptionCommand>
{
    public UpdateTenantSubscriptionCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("tenantId obligatorio.");

        RuleFor(x => x.Justification)
            .NotEmpty()
            .MinimumLength(5)
            .WithMessage("La justificación debe tener al menos 5 caracteres.");

        RuleFor(x => x.PlanCode)
            .NotEmpty()
            .WithMessage("planCode obligatorio.")
            .Must(TenantPlanPresets.IsKnown)
            .WithMessage("planCode debe ser Starter, Pro o Unlimited.");

        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage("status inválido.");

        RuleFor(x => x.BillingCycle)
            .IsInEnum()
            .WithMessage("billingCycle inválido.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .When(x => x.Notes is not null);

        RuleFor(x => x)
            .Must(x =>
            {
                if (!x.CurrentPeriodStartUtc.HasValue || !x.CurrentPeriodEndUtc.HasValue)
                    return true;
                return x.CurrentPeriodEndUtc.Value > x.CurrentPeriodStartUtc.Value;
            })
            .WithMessage("currentPeriodEndUtc debe ser posterior a currentPeriodStartUtc.");

        RuleFor(x => x.TrialEndsAtUtc)
            .Must(v => !v.HasValue || v.Value.Kind == DateTimeKind.Utc || v.Value.Kind == DateTimeKind.Unspecified)
            .WithMessage("trialEndsAtUtc inválido.");
    }
}
