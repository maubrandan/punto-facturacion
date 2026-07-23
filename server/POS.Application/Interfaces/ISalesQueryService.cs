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
}
