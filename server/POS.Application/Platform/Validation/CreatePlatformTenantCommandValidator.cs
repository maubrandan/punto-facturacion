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
    }
}
