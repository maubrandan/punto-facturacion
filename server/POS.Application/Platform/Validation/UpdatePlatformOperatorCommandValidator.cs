using FluentValidation;
using POS.Application.Platform;
using POS.Domain.Platform;

namespace POS.Application.Platform.Validation;

public sealed class UpdatePlatformOperatorCommandValidator : AbstractValidator<UpdatePlatformOperatorCommand>
{
    public UpdatePlatformOperatorCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(128);

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(x => x.PlatformRole)
            .NotEmpty()
            .Must(PlatformRoleNames.IsKnownRole)
            .WithMessage("El rol debe ser un nombre Platform.* válido.");
    }
}
