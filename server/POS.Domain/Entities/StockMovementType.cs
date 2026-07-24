namespace POS.Domain.Entities;

public enum StockMovementType
{
    Sale = 1,
    Purchase = 2,
    Adjustment = 3,
    ProductSeed = 4,
    /// <summary>Reposición de stock por devolución de venta.</summary>
    SaleReturn = 5
}
