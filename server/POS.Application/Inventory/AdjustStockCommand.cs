namespace POS.Application.Inventory;

public sealed record AdjustStockCommand(
    Guid ProductId,
    decimal QuantityDelta,
    string Reason,
    Guid? StockLotId = null,
    string? LotNumber = null,
    DateOnly? ExpirationDate = null);
