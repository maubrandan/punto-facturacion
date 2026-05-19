using FluentValidation;
using POS.Application.Contracts.Platform;

namespace POS.Application.Platform.Validation;

public sealed class PlatformUserActionRequestValidator : AbstractValidator<PlatformUserActionRequest>
{
    public PlatformUserActionRequestValidator()
    {
        RuleFor(x => x.Justification)
            .NotEmpty()
            .MinimumLength(5)
            .MaximumLength(2000);
    }
}
