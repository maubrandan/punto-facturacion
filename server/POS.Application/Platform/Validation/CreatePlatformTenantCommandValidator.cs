using FluentValidation;
using POS.Application.Platform;
using POS.Domain.Entities;

namespace POS.Application.Platform.Validation;

public sealed class CreatePlatformTenantCommandValidator : AbstractValidator<CreatePlatformTenantCommand>
{
    public CreatePlatformTenantCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(x => x.ContactEmail)
            .MaximumLength(320)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));

        RuleFor(x => x.BusinessType)
            .NotEmpty()
            .Must(BusinessTypeNames.IsKnown)
            .WithMessage("El rubro debe ser Farmacia, Ferreteria o Kiosco.");

        RuleFor(x => x.AdminEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(x => x.AdminFullName)
            .MaximumLength(512);

        RuleFor(x => x.AdminPassword)
            .NotEmpty()
            .MinimumLength(6)
            .MaximumLength(128);

        RuleFor(x => x.PlanCode)
            .Must(v => string.IsNullOrWhiteSpace(v) || TenantPlanPresets.IsKnown(v))
            .WithMessage("El plan debe ser Starter, Pro o Unlimited.");

        RuleFor(x => x.MaxProducts)
            .Must(v => !v.HasValue || v.Value >= 1)
            .WithMessage("MaxProducts debe ser >= 1 o null (sin límite).");

        RuleFor(x => x.MaxTenantUsers)
            .Must(v => !v.HasValue || v.Value >= 1)
            .WithMessage("MaxTenantUsers debe ser >= 1 o null (sin límite).");
    }
}
