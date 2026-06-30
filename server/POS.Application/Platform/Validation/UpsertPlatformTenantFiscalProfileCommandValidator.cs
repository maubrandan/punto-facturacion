using FluentValidation;

namespace POS.Application.Platform.Validation;

public sealed class UpsertPlatformTenantFiscalProfileCommandValidator
    : AbstractValidator<UpsertPlatformTenantFiscalProfileCommand>
{
    public UpsertPlatformTenantFiscalProfileCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Justification).MinimumLength(5);
        RuleFor(x => x.Values.TaxId)
            .Must(t => new string(t.Where(char.IsDigit).ToArray()).Length == 11)
            .WithMessage("El CUIT debe tener 11 dígitos.");
        RuleFor(x => x.Values.PointOfSale).InclusiveBetween(1, 99999);
    }
}
