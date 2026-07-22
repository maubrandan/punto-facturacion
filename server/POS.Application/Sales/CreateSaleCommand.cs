namespace POS.Application.Sales;

public sealed record CreateSaleLineCommand(
    Guid ProductId,
    decimal Quantity,
    Guid? StockLotId = null);

public sealed record CreateSalePaymentCommand(int Method, decimal Amount);

public sealed record CreateSaleCommand(
    IReadOnlyList<CreateSaleLineCommand> Lines,
    IReadOnlyList<CreateSalePaymentCommand> Payments);
