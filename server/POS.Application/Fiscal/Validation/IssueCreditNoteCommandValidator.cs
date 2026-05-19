using FluentValidation;

namespace POS.Application.Fiscal.Validation;

public sealed class IssueCreditNoteCommandValidator : AbstractValidator<IssueCreditNoteCommand>
{
    public IssueCreditNoteCommandValidator()
    {
        RuleFor(x => x.OriginalFiscalDocumentId).NotEmpty();
        RuleFor(x => x.SaleId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
    }
}
