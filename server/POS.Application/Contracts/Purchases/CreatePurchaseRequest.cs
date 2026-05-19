namespace POS.Application.Contracts.Purchases;

public sealed class CreatePurchaseLineRequest
{
    public Guid ProductId { get; init; }

    public int Quantity { get; init; }

    public decimal UnitCost { get; init; }
}

public sealed class CreatePurchaseRequest
{
    public Guid ProviderId { get; init; }

    public DateTime Date { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public IReadOnlyList<CreatePurchaseLineRequest> Lines { get; init; } = Array.Empty<CreatePurchaseLineRequest>();
}
