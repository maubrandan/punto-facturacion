namespace POS.Domain.Entities;

/// <summary>Estado de devolución comercial de una venta (v1: total o ninguna).</summary>
public enum SaleReturnStatus
{
    None = 0,
    FullyReturned = 1
}
