namespace POS.Application.Sales;

/// <summary>Devolución total de una venta (v1: sin líneas parciales).</summary>
public sealed record CreateSaleReturnCommand(Guid SaleId);
