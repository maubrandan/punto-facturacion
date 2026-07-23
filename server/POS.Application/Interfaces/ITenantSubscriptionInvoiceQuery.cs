using POS.Application.Common;
using POS.Application.Contracts.Platform;

namespace POS.Application.Interfaces;

/// <summary>Facturas SaaS del tenant del JWT actual.</summary>
public interface ITenantSubscriptionInvoiceQuery
{
    Task<Result<SubscriptionInvoiceListDto>> ListForCurrentTenantAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<SubscriptionInvoiceDto>> GetForCurrentTenantAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default);
}
