namespace POS.Application.Contracts.Sales;

/// <summary>
/// Reporte de ventas para un rango de fechas (calendario UTC, inclusive por día).
/// Incluye totales, breakdown por medio de pago y por cajero.
/// </summary>
public sealed class SalesReportResponse
{
    public DateTime StartDate { get; init; }

    public DateTime EndDate { get; init; }

    public decimal TotalSalesAmount { get; init; }

    public int SalesCount { get; init; }

    public IReadOnlyList<SalesReportPaymentBreakdownItem> ByPaymentMethod { get; init; } =
        Array.Empty<SalesReportPaymentBreakdownItem>();

    public IReadOnlyList<SalesReportCashierBreakdownItem> ByCashier { get; init; } =
        Array.Empty<SalesReportCashierBreakdownItem>();
}

public sealed class SalesReportPaymentBreakdownItem
{
    /// <summary>Código de <see cref="POS.Domain.Entities.PaymentMethod"/>.</summary>
    public int Method { get; init; }

    public decimal Amount { get; init; }

    public int PaymentCount { get; init; }
}

public sealed class SalesReportCashierBreakdownItem
{
    public string? CreatedByUserId { get; init; }

    public string CreatedByUserName { get; init; } = "—";

    public decimal TotalAmount { get; init; }

    public int SalesCount { get; init; }
}
