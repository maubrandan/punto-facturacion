using POS.Application.Common;
using POS.Application.Contracts.Customers;

namespace POS.Application.Interfaces;

public interface ICustomerAccountQueryService
{
    Task<Result<CustomerAccountResponse>> GetAccountAsync(
        Guid customerId,
        int recentLimit = 20,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CustomerAccountMovementResponse>>> ListMovementsAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);
}

public interface IRegisterCustomerAccountPaymentHandler
{
    Task<Result<RegisterCustomerAccountPaymentResponse>> HandleAsync(
        Guid customerId,
        RegisterCustomerAccountPaymentRequest request,
        CancellationToken cancellationToken = default);
}
