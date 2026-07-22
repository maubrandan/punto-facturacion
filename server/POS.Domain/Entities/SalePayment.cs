using POS.Domain.Common;

namespace POS.Domain.Entities;

/// <summary>Línea de cobro de una venta (permite split efectivo + tarjeta/transferencia).</summary>
public sealed class SalePayment : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public Guid SaleId { get; set; }

    public Sale? Sale { get; set; }

    public PaymentMethod Method { get; set; }

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>True si el monto debe entrar al arqueo de efectivo de la caja.</summary>
    public static bool AffectsCashDrawer(PaymentMethod method) => method == PaymentMethod.Cash;
}
