namespace POS.Application.Contracts.Sales;

public sealed class CreateSaleRequest
{
    public required IReadOnlyList<CreateSaleLineRequest> Lines { get; init; }

    public IReadOnlyList<CreateSalePaymentRequest> Payments { get; init; } = Array.Empty<CreateSalePaymentRequest>();

    /// <summary>Obligatorio si algún cobro usa cuenta corriente (Credit = 3).</summary>
    public Guid? CustomerId { get; init; }
}

public sealed class CreateSaleLineRequest
{
    public Guid ProductId { get; init; }

    public decimal Quantity { get; init; }

    public Guid? StockLotId { get; init; }
}

public sealed class CreateSalePaymentRequest
{
    /// <summary>0 = Cash, 1 = Card, 2 = Transfer, 3 = Credit (cuenta corriente).</summary>
    public int Method { get; init; }

    public decimal Amount { get; init; }
}
