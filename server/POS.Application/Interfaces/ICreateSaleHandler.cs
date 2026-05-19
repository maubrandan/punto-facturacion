using POS.Application.Common;
using POS.Application.Contracts.Sales;
using POS.Application.Sales;

namespace POS.Application.Interfaces;

public interface ICreateSaleHandler
{
    Task<Result<SaleResponse>> HandleAsync(CreateSaleCommand command, CancellationToken cancellationToken = default);
}
