namespace POS.Application.Contracts.Purchases;

public sealed class PurchaseLineResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductSku { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitCost { get; init; }

    public decimal Subtotal { get; init; }
}

public sealed class PurchaseResponse
{
    public Guid Id { get; init; }

    public Guid ProviderId { get; init; }

    public string ProviderName { get; init; } = string.Empty;

    public DateTime Date { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public IReadOnlyList<PurchaseLineResponse> Lines { get; init; } = Array.Empty<PurchaseLineResponse>();
}

public sealed class PurchaseSummaryResponse
{
    public Guid Id { get; init; }

    public Guid ProviderId { get; init; }

    public string ProviderName { get; init; } = string.Empty;

    public DateTime Date { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public decimal Total { get; init; }
}
