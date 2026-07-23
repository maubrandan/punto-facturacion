using FluentValidation;
using POS.Application.Platform;

namespace POS.Application.Platform.Validation;

public sealed class BlockPlatformOperatorCommandValidator : AbstractValidator<BlockPlatformOperatorCommand>
{
    public BlockPlatformOperatorCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(128);

        RuleFor(x => x.Justification)
            .NotEmpty()
            .MinimumLength(5)
            .MaximumLength(2000);
    }
}
