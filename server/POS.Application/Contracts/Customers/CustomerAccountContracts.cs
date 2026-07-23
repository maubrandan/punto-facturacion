namespace POS.Application.Contracts.Customers;

public sealed class CustomerAccountMovementResponse
{
    public Guid Id { get; init; }

    public int Type { get; init; }

    public decimal Amount { get; init; }

    public decimal BalanceAfter { get; init; }

    public Guid? SaleId { get; init; }

    public string? Notes { get; init; }

    public int? SettlementMethod { get; init; }

    public Guid? CashSessionId { get; init; }

    public string? CreatedByUserId { get; init; }

    public DateTime CreatedAt { get; init; }
}

public sealed class CustomerAccountResponse
{
    public Guid CustomerId { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    /// <summary>Saldo actual (positivo = deuda del cliente).</summary>
    public decimal Balance { get; init; }

    public IReadOnlyList<CustomerAccountMovementResponse> RecentMovements { get; init; } =
        Array.Empty<CustomerAccountMovementResponse>();
}

public sealed class RegisterCustomerAccountPaymentRequest
{
    public decimal Amount { get; init; }

    /// <summary>0 = Cash, 1 = Card, 2 = Transfer (no Credit).</summary>
    public int Method { get; init; }

    public string? Notes { get; init; }
}

public sealed class RegisterCustomerAccountPaymentResponse
{
    public Guid MovementId { get; init; }

    public decimal Amount { get; init; }

    public decimal BalanceAfter { get; init; }

    public int SettlementMethod { get; init; }

    public Guid? CashSessionId { get; init; }

    public DateTime CreatedAt { get; init; }
}
