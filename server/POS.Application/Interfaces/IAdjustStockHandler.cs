using POS.Application.Common;
using POS.Application.Contracts.Inventory;
using POS.Application.Inventory;

namespace POS.Application.Interfaces;

public interface IAdjustStockHandler
{
    Task<Result<StockAdjustmentResponse>> HandleAsync(
        AdjustStockCommand command,
        CancellationToken cancellationToken = default);
}

public interface IInventoryQueryService
{
    Task<Result<IReadOnlyList<StockLotResponse>>> GetLotsForProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<Result<PagedStockMovementsResponse>> GetMovementsAsync(
        Guid? productId,
        int page,
        int pageSize,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AdjustmentReasonOptionResponse>>> GetAdjustmentReasonsAsync(
        CancellationToken cancellationToken = default);

    Task<Result<ExpiryAlertsResponse>> GetExpiryAlertsAsync(
        int? withinDays = null,
        CancellationToken cancellationToken = default);
}
