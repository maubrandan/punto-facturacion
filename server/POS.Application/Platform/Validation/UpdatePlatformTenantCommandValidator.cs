using FluentValidation;
using POS.Application.Platform;

namespace POS.Application.Platform.Validation;

public sealed class UpdatePlatformTenantCommandValidator : AbstractValidator<UpdatePlatformTenantCommand>
{
    public UpdatePlatformTenantCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(x => x.ContactEmail)
            .MaximumLength(320)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
    }
}
