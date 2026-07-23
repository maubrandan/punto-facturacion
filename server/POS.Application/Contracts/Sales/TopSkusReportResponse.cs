namespace POS.Application.Contracts.Sales;

public sealed class TopSkusReportResponse
{
    public DateTime StartDate { get; init; }

    public DateTime EndDate { get; init; }

    /// <summary><c>quantity</c> o <c>revenue</c>.</summary>
    public string SortBy { get; init; } = "quantity";

    public IReadOnlyList<TopSkuReportItem> Items { get; init; } = Array.Empty<TopSkuReportItem>();
}

public sealed class TopSkuReportItem
{
    public Guid ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public decimal RevenueNet { get; init; }

    public decimal RevenueTotal { get; init; }
}
