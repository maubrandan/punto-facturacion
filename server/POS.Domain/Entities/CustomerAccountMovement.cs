using POS.Domain.Common;

namespace POS.Domain.Entities;

/// <summary>Movimiento de cuenta corriente del cliente (cargo o cobro de deuda).</summary>
public sealed class CustomerAccountMovement : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public CustomerAccountMovementType Type { get; set; }

    /// <summary>Firmado: positivo = cargo/deuda; negativo = pago/cobro.</summary>
    public decimal Amount { get; set; }

    public decimal BalanceAfter { get; set; }

    public Guid? SaleId { get; set; }

    public Sale? Sale { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Medio usado al cobrar deuda (Cash/Card/Transfer). Null en cargos por venta a crédito.
    /// </summary>
    public PaymentMethod? SettlementMethod { get; set; }

    public Guid? CashSessionId { get; set; }

    public CashSession? CashSession { get; set; }

    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
