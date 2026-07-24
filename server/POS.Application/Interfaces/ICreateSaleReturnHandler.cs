using POS.Application.Common;
using POS.Application.Contracts.Sales;
using POS.Application.Sales;

namespace POS.Application.Interfaces;

public interface ICreateSaleReturnHandler
{
    Task<Result<SaleReturnResponse>> HandleAsync(
        CreateSaleReturnCommand command,
        CancellationToken cancellationToken = default);
}
