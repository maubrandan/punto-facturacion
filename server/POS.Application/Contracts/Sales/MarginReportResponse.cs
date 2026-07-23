namespace POS.Application.Contracts.Sales;

/// <summary>
/// Margen aproximado (neto) usando <c>Product.LastCost</c> actual — no es COGS histórico al momento de la venta.
/// </summary>
public sealed class MarginReportResponse
{
    public DateTime StartDate { get; init; }

    public DateTime EndDate { get; init; }

    /// <summary>Ingresos netos de todas las líneas del rango.</summary>
    public decimal RevenueNet { get; init; }

    /// <summary>Ingresos netos solo de líneas con <c>LastCost</c>.</summary>
    public decimal RevenueNetWithCost { get; init; }

    /// <summary>Ingresos netos de líneas sin costo disponible.</summary>
    public decimal RevenueNetWithoutCost { get; init; }

    /// <summary>Costo neto estimado (cantidad × LastCost) de líneas con costo.</summary>
    public decimal CostNet { get; init; }

    /// <summary>RevenueNetWithCost − CostNet.</summary>
    public decimal MarginNet { get; init; }

    public int LinesWithCost { get; init; }

    public int LinesWithoutCost { get; init; }

    public IReadOnlyList<MarginReportSkuItem> BySku { get; init; } = Array.Empty<MarginReportSkuItem>();
}

public sealed class MarginReportSkuItem
{
    public Guid ProductId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public decimal RevenueNet { get; init; }

    public decimal? CostNet { get; init; }

    public decimal? MarginNet { get; init; }

    public bool HasCost { get; init; }
}
