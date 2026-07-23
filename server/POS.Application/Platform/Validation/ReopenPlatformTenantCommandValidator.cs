using FluentValidation;
using POS.Application.Platform;

namespace POS.Application.Platform.Validation;

public sealed class ReopenPlatformTenantCommandValidator : AbstractValidator<ReopenPlatformTenantCommand>
{
    public ReopenPlatformTenantCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().MaximumLength(128);

        RuleFor(x => x.Justification)
            .NotEmpty()
            .MinimumLength(5)
            .MaximumLength(2000);
    }
}
