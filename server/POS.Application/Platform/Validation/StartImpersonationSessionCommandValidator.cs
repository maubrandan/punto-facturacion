using FluentValidation;

namespace POS.Application.Platform.Validation;

public sealed class StartImpersonationSessionCommandValidator : AbstractValidator<StartImpersonationSessionCommand>
{
    public StartImpersonationSessionCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().MaximumLength(128);

        RuleFor(x => x.Reason).NotEmpty().MinimumLength(5).MaximumLength(2000);

        RuleFor(x => x.TtlMinutes).InclusiveBetween(1, 60);
    }
}
