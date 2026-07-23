using FluentValidation;
using POS.Domain.Entities;

namespace POS.Application.Inventory.Validation;

public sealed class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.QuantityDelta).NotEqual(0m)
            .WithErrorCode("stock.adjustment")
            .WithMessage("El ajuste no puede ser cero.");
        RuleFor(x => x.ReasonCode)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("stock.reason_invalid")
            .WithMessage("Debe indicar un motivo de ajuste válido.")
            .Must(StockAdjustmentReasonCodes.IsKnown)
            .WithErrorCode("stock.reason_invalid")
            .WithMessage("El motivo del ajuste no es válido.");
        RuleFor(x => x.Note).MaximumLength(512)
            .WithErrorCode("stock.adjustment")
            .WithMessage("La nota no puede superar 512 caracteres.");
    }
}
