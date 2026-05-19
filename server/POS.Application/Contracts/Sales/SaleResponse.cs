namespace POS.Application.Contracts.Sales;

public sealed class SaleLineResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public int Quantity { get; init; }

    public decimal LineNetSubtotal { get; init; }

    public decimal LineTaxAmount { get; init; }

    public decimal UnitNetPrice { get; init; }

    public decimal TaxRate { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductExtendedDataJson { get; init; } = "{}";
}

public sealed class SaleResponse
{
    public Guid Id { get; init; }

    public DateTime Date { get; init; }

    public decimal TotalNet { get; init; }

    public decimal TotalTax { get; init; }

    public decimal TotalAmount { get; init; }

    public IReadOnlyList<SaleLineResponse> Lines { get; init; } = Array.Empty<SaleLineResponse>();
}
