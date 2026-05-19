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
}
