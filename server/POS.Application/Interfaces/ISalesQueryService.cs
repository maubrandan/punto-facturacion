using POS.Application.Common;
using POS.Application.Contracts.Sales;

namespace POS.Application.Interfaces;

public interface ISalesQueryService
{
    Task<PagedSalesResponse> GetPagedAsync(
        DateTime? startDate,
        DateTime? endDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SaleDetailViewResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SaleReturnResponse>> GetReturnsBySaleIdAsync(
        Guid saleId,
        CancellationToken cancellationToken = default);

    /// <param name="dateUtc">Día a consultar (solo la parte de fecha, interpretado en UTC). Si null, hoy en UTC.</param>
    Task<DailySummaryResponse> GetDailySummaryAsync(DateTime? dateUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reporte de ventas en rango (UTC, inclusive por día calendario).
    /// Si no se envían fechas, usa el día de hoy (UTC).
    /// </summary>
    Task<SalesReportResponse> GetSalesReportAsync(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Margen aproximado (neto vs <c>Product.LastCost</c> actual) para un rango UTC inclusive por día.
    /// </summary>
    Task<Result<MarginReportResponse>> GetMarginReportAsync(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Top SKUs por cantidad o ingresos netos en un rango UTC inclusive por día.
    /// </summary>
    /// <param name="sortBy"><c>quantity</c> (default) o <c>revenue</c>.</param>
    /// <param name="take">Cantidad de filas (default 10, máx. 50).</param>
    Task<Result<TopSkusReportResponse>> GetTopSkusReportAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? sortBy,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ventas agregadas por día / semana (lunes UTC) / mes en un rango UTC inclusive por día.
    /// </summary>
    /// <param name="period"><c>day</c> (default), <c>week</c> o <c>month</c>.</param>
    Task<Result<SalesByPeriodReportResponse>> GetSalesByPeriodReportAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default);
}
