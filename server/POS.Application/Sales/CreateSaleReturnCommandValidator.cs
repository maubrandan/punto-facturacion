using FluentValidation;

namespace POS.Application.Sales;

public sealed class CreateSaleReturnCommandValidator : AbstractValidator<CreateSaleReturnCommand>
{
    public CreateSaleReturnCommandValidator()
    {
        RuleFor(x => x.SaleId).NotEmpty().WithMessage("Debe indicar la venta a devolver.");
    }
}
