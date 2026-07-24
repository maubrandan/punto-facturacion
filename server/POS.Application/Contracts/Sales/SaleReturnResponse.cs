namespace POS.Application.Contracts.Sales;

public sealed class SaleReturnLineResponse
{
    public Guid Id { get; init; }

    public Guid SaleDetailId { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public Guid? StockLotId { get; init; }

    public decimal LineNetSubtotal { get; init; }

    public decimal LineTaxAmount { get; init; }
}

public sealed class SaleReturnResponse
{
    public Guid Id { get; init; }

    public Guid SaleId { get; init; }

    public DateTime ReturnedAt { get; init; }

    public decimal TotalAmount { get; init; }

    public string? CreatedByUserName { get; init; }

    public Guid? CashSessionId { get; init; }

    public Guid? FiscalDocumentId { get; init; }

    public IReadOnlyList<SaleReturnLineResponse> Lines { get; init; } = Array.Empty<SaleReturnLineResponse>();

    public IReadOnlyList<SalePaymentResponse> Payments { get; init; } = Array.Empty<SalePaymentResponse>();
}
