using FluentValidation;
using POS.Application.Platform;

namespace POS.Application.Platform.Validation;

public sealed class SetTenantEntitlementsCommandValidator : AbstractValidator<SetTenantEntitlementsCommand>
{
    public SetTenantEntitlementsCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("tenantId obligatorio.");

        RuleFor(x => x.Justification)
            .NotEmpty()
            .MinimumLength(5)
            .WithMessage("La justificación debe tener al menos 5 caracteres.");

        RuleFor(x => x.MaxProducts)
            .Must(v => !v.HasValue || v.Value >= 1)
            .WithMessage("MaxProducts debe ser >= 1 o null (sin límite).");

        RuleFor(x => x.MaxTenantUsers)
            .Must(v => !v.HasValue || v.Value >= 1)
            .WithMessage("MaxTenantUsers debe ser >= 1 o null (sin límite).");
    }
}
