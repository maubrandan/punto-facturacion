namespace POS.Application.Contracts.Cash;

public sealed class CashSessionOpenResponse
{
    public Guid Id { get; init; }

    public DateTime OpeningDate { get; init; }

    public decimal InitialAmount { get; init; }

    public int State { get; init; }

    public string? UserId { get; init; }
}

public sealed class CashSessionCloseResponse
{
    public Guid Id { get; init; }

    public decimal InitialAmount { get; init; }

    public decimal ExpectedAmount { get; init; }

    public decimal CountedAmount { get; init; }

    public decimal Difference { get; init; }

    public DateTime? ClosingDate { get; init; }
}

public sealed class CashSessionSummaryResponse
{
    public Guid? SessionId { get; init; }

    public DateTime? OpeningDate { get; init; }

    public decimal? InitialAmount { get; init; }

    public decimal TotalSales { get; init; }

    /// <summary>Suma de cobros en efectivo de las ventas del turno (entra al cajón).</summary>
    public decimal TotalCashPayments { get; init; }

    /// <summary>Suma de cobros con tarjeta en el turno (informativo).</summary>
    public decimal TotalCardPayments { get; init; }

    /// <summary>Suma de cobros por transferencia en el turno (informativo).</summary>
    public decimal TotalTransferPayments { get; init; }

    public decimal TotalPurchases { get; init; }

    public decimal TotalExpenses { get; init; }

    /// <summary>Saldo teórico en caja: Initial + cobros efectivo - compras - gastos.</summary>
    public decimal ProjectedAmount { get; init; }
}

public sealed class ExpenseCategoryResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;
}

public sealed class CreateExpenseCategoryRequest
{
    public required string Name { get; init; }
}

public sealed class RegisterExpenseRequest
{
    public required string Description { get; init; }

    public decimal Amount { get; init; }

    public DateTime? Date { get; init; }

    public Guid CategoryId { get; init; }
}

public sealed class ExpenseResponse
{
    public Guid Id { get; init; }

    public string Description { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateTime Date { get; init; }

    public Guid CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public Guid? CashSessionId { get; init; }
}

public sealed class OpenSessionRequest
{
    public decimal InitialAmount { get; init; }
}

public sealed class CloseSessionRequest
{
    public decimal CountedAmount { get; init; }
}
