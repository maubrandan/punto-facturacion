namespace POS.Application.Purchases;

public sealed record CreatePurchaseLineCommand(Guid ProductId, int Quantity, decimal UnitCost);

public sealed record CreatePurchaseCommand(
    Guid ProviderId,
    DateTime Date,
    string InvoiceNumber,
    IReadOnlyList<CreatePurchaseLineCommand> Lines);
