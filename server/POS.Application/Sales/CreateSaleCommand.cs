namespace POS.Application.Sales;

public sealed record CreateSaleLineCommand(Guid ProductId, int Quantity);

public sealed record CreateSaleCommand(IReadOnlyList<CreateSaleLineCommand> Lines);
