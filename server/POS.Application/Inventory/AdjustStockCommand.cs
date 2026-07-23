namespace POS.Application.Inventory;

public sealed record AdjustStockCommand(
    Guid ProductId,
    decimal QuantityDelta,
    string ReasonCode,
    string? Note = null,
    Guid? StockLotId = null,
    string? LotNumber = null,
    DateOnly? ExpirationDate = null);
