namespace POS.Domain.Entities;

/// <summary>Tipo de movimiento en cuenta corriente del cliente.</summary>
public enum CustomerAccountMovementType
{
    /// <summary>Cargo por venta a crédito (aumenta deuda).</summary>
    Charge = 0,

    /// <summary>Cobro / pago de deuda (reduce saldo).</summary>
    Payment = 1
}
