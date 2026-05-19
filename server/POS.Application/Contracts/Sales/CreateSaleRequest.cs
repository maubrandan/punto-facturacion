namespace POS.Application.Contracts.Sales;

public sealed class CreateSaleRequest
{
    public required IReadOnlyList<CreateSaleLineRequest> Lines { get; init; }
}

public sealed class CreateSaleLineRequest
{
    public Guid ProductId { get; init; }

    public int Quantity { get; init; }
}
