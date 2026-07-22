namespace POS.Application.Contracts.Products;

public sealed class ProductCreateRequest
{
    public required string Name { get; init; }

    public required string SKU { get; init; }

    public string Barcode { get; init; } = string.Empty;

    public decimal NetPrice { get; init; }

    public decimal TaxRate { get; init; }

    public decimal Stock { get; init; }

    public string ExtendedDataJson { get; init; } = "{}";

    /// <summary>Farmacia: lote inicial si Stock &gt; 0.</summary>
    public string? LotNumber { get; init; }

    public DateOnly? ExpirationDate { get; init; }
}
