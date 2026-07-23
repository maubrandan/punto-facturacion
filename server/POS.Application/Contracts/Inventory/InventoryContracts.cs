namespace POS.Application.Contracts.Inventory;

public sealed class StockLotResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public string LotNumber { get; init; } = string.Empty;

    public DateOnly ExpirationDate { get; init; }

    public decimal Quantity { get; init; }

    public bool IsExpired { get; init; }
}

public sealed class StockMovementResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public decimal QuantityDelta { get; init; }

    public decimal QuantityAfter { get; init; }

    public Guid? StockLotId { get; init; }

    public string? LotNumberSnapshot { get; init; }

    public DateOnly? ExpirationSnapshot { get; init; }

    public string? ReasonCode { get; init; }

    public string? ReasonNote { get; init; }

    public Guid? ReferenceId { get; init; }

    public DateTime CreatedAt { get; init; }
}

public sealed class PagedStockMovementsResponse
{
    public IReadOnlyList<StockMovementResponse> Items { get; init; } = Array.Empty<StockMovementResponse>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}

public sealed class StockAdjustmentResponse
{
    public Guid ProductId { get; init; }

    public decimal StockAfter { get; init; }

    public Guid? StockLotId { get; init; }

    public string? LotNumber { get; init; }

    public decimal QuantityDelta { get; init; }

    public string ReasonCode { get; init; } = string.Empty;
}

public sealed class AdjustmentReasonOptionResponse
{
    public string Code { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;
}

public sealed class ExpiryAlertItemResponse
{
    public Guid StockLotId { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string LotNumber { get; init; } = string.Empty;

    public DateOnly ExpirationDate { get; init; }

    public decimal Quantity { get; init; }

    public string Status { get; init; } = string.Empty;

    public int DaysToExpiration { get; init; }
}

public sealed class ExpiryAlertsResponse
{
    /// <summary>False para rubros sin lotes/vencimiento (Ferretería, Kiosco).</summary>
    public bool Supported { get; init; }

    public int WithinDays { get; init; }

    public IReadOnlyList<ExpiryAlertItemResponse> Items { get; init; } = Array.Empty<ExpiryAlertItemResponse>();
}
