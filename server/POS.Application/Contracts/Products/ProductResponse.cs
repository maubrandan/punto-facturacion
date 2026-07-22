using POS.Domain.Entities;

namespace POS.Application.Contracts.Products;

public sealed class ProductResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string SKU { get; init; } = string.Empty;

    public string Barcode { get; init; } = string.Empty;

    public decimal NetPrice { get; init; }

    public decimal TaxRate { get; init; }

    public decimal FinalPrice { get; init; }

    public decimal Stock { get; init; }

    public decimal? LastCost { get; init; }

    public string ExtendedDataJson { get; init; } = "{}";

    public DateTime CreatedAt { get; init; }

    public static ProductResponse FromEntity(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        SKU = p.SKU,
        Barcode = p.Barcode,
        NetPrice = p.NetPrice,
        TaxRate = p.TaxRate,
        FinalPrice = p.FinalPrice,
        Stock = p.Stock,
        LastCost = p.LastCost,
        ExtendedDataJson = p.ExtendedDataJson,
        CreatedAt = p.CreatedAt
    };
}
