using FluentValidation;

namespace POS.Application.Fiscal.Validation;

public sealed class RetryElectronicInvoiceCommandValidator : AbstractValidator<RetryElectronicInvoiceCommand>
{
    public RetryElectronicInvoiceCommandValidator()
    {
        RuleFor(x => x.FiscalDocumentId).NotEmpty();
    }
}
