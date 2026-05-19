using FluentValidation;

namespace POS.Application.Fiscal.Validation;

public sealed class IssueElectronicInvoiceCommandValidator : AbstractValidator<IssueElectronicInvoiceCommand>
{
    public IssueElectronicInvoiceCommandValidator()
    {
        RuleFor(x => x.SaleId).NotEmpty();
    }
}
