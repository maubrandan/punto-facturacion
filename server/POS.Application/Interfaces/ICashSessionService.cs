using POS.Application.Common;
using POS.Application.Contracts.Cash;

namespace POS.Application.Interfaces;

public interface ICashSessionService
{
    /// <summary>Id de la sesión abierta del tenant, o null si no hay caja.</summary>
    Task<Guid?> GetOpenSessionIdAsync(CancellationToken cancellationToken = default);

    Task<Result<CashSessionOpenResponse>> OpenSessionAsync(
        decimal initialAmount,
        CancellationToken cancellationToken = default);

    Task<Result<CashSessionCloseResponse>> CloseSessionAsync(
        decimal countedAmount,
        CancellationToken cancellationToken = default);

    Task<Result<ExpenseResponse>> RegisterExpenseAsync(
        RegisterExpenseRequest request,
        CancellationToken cancellationToken = default);

    Task<CashSessionSummaryResponse> GetCurrentSummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpenseCategoryResponse>> ListCategoriesAsync(
        CancellationToken cancellationToken = default);

    Task<Result<ExpenseCategoryResponse>> CreateCategoryAsync(
        string name,
        CancellationToken cancellationToken = default);
}
