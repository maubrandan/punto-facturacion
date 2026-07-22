namespace POS.Domain.Entities;

/// <summary>Medio de cobro de una venta POS.</summary>
public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
    Transfer = 2
}
