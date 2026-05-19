using POS.Application.Common;
using POS.Application.Contracts.Purchases;
using POS.Application.Purchases;

namespace POS.Application.Interfaces;

public interface ICreatePurchaseHandler
{
    Task<Result<PurchaseResponse>> HandleAsync(
        CreatePurchaseCommand command,
        CancellationToken cancellationToken = default);
}
