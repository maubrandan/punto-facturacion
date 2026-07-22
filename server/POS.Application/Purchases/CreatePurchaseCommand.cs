namespace POS.Application.Purchases;

public sealed record CreatePurchaseLineCommand(
    Guid ProductId,
    decimal Quantity,
    decimal UnitCost,
    string? LotNumber = null,
    DateOnly? ExpirationDate = null);

public sealed record CreatePurchaseCommand(
    Guid ProviderId,
    DateTime Date,
    string InvoiceNumber,
    IReadOnlyList<CreatePurchaseLineCommand> Lines);
