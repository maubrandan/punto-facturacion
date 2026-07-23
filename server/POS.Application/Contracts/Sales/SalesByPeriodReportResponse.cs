namespace POS.Application.Contracts.Sales;

public sealed class SalesByPeriodReportResponse
{
    public DateTime StartDate { get; init; }

    public DateTime EndDate { get; init; }

    /// <summary><c>day</c>, <c>week</c> (lunes UTC) o <c>month</c>.</summary>
    public string Period { get; init; } = "day";

    public decimal TotalSalesAmount { get; init; }

    public int SalesCount { get; init; }

    public IReadOnlyList<SalesByPeriodBucketItem> Buckets { get; init; } = Array.Empty<SalesByPeriodBucketItem>();
}

public sealed class SalesByPeriodBucketItem
{
    public DateTime PeriodStart { get; init; }

    public DateTime PeriodEnd { get; init; }

    public decimal TotalSalesAmount { get; init; }

    public int SalesCount { get; init; }
}
