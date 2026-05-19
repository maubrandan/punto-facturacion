using FluentValidation;
using POS.Domain.Platform;

namespace POS.Application.Platform.Validation;

public sealed class ProvisionPlatformUserCommandValidator : AbstractValidator<ProvisionPlatformUserCommand>
{
    public ProvisionPlatformUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(4);

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(512)
            .MinimumLength(1);

        RuleFor(x => x.PlatformRole)
            .NotEmpty()
            .Must(PlatformRoleNames.IsKnownRole)
            .WithMessage("El rol debe ser un nombre Platform.* válido (ver PlatformRoleNames).");
    }
}
