using FluentValidation;

namespace POS.Application.Fiscal.Validation;

public sealed class IssueElectronicInvoiceCommandValidator : AbstractValidator<IssueElectronicInvoiceCommand>
{
    public IssueElectronicInvoiceCommandValidator()
    {
        RuleFor(x => x.SaleId).NotEmpty();
        RuleFor(x => x.BuyerTaxId)
            .NotEmpty()
            .When(x => x.IsInvoiceA)
            .WithMessage("Factura A requiere CUIT del comprador.");
        RuleFor(x => x.BuyerTaxId)
            .Must(BeValidCuit)
            .When(x => x.IsInvoiceA && !string.IsNullOrWhiteSpace(x.BuyerTaxId))
            .WithMessage("El CUIT del comprador debe tener 11 dígitos.");
    }

    private static bool BeValidCuit(string? taxId)
    {
        if (string.IsNullOrWhiteSpace(taxId))
            return false;
        var digits = new string(taxId.Where(char.IsDigit).ToArray());
        return digits.Length == 11;
    }
}
