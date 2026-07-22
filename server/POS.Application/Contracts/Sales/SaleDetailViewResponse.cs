using POS.Application.Contracts.Fiscal;

namespace POS.Application.Contracts.Sales;

public sealed class SaleDetailLineViewResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductExtendedDataJson { get; init; } = "{}";

    public decimal Quantity { get; init; }

    public decimal LineNetSubtotal { get; init; }

    public decimal LineTaxAmount { get; init; }

    public decimal LineTotal { get; init; }
}

public sealed class SaleDetailViewResponse
{
    public Guid Id { get; init; }

    public DateTime Date { get; init; }

    public decimal TotalNet { get; init; }

    public decimal TotalTax { get; init; }

    public decimal TotalAmount { get; init; }

    public string? CreatedByUserName { get; init; }

    public IReadOnlyList<SaleDetailLineViewResponse> Lines { get; init; } = Array.Empty<SaleDetailLineViewResponse>();

    public IReadOnlyList<SalePaymentResponse> Payments { get; init; } = Array.Empty<SalePaymentResponse>();

    public IReadOnlyList<FiscalDocumentResponse> FiscalDocuments { get; init; } = Array.Empty<FiscalDocumentResponse>();
}
