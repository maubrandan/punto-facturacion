namespace POS.Application.Contracts.Sales;

public sealed class CreateSaleRequest
{
    public required IReadOnlyList<CreateSaleLineRequest> Lines { get; init; }

    public IReadOnlyList<CreateSalePaymentRequest> Payments { get; init; } = Array.Empty<CreateSalePaymentRequest>();
}

public sealed class CreateSaleLineRequest
{
    public Guid ProductId { get; init; }

    public decimal Quantity { get; init; }

    public Guid? StockLotId { get; init; }
}

public sealed class CreateSalePaymentRequest
{
    /// <summary>0 = Cash, 1 = Card, 2 = Transfer.</summary>
    public int Method { get; init; }

    public decimal Amount { get; init; }
}
