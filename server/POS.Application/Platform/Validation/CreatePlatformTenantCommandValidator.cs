using FluentValidation;
using POS.Application.Platform;

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
    }
}
