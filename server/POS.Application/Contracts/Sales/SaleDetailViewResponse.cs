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

    /// <summary>Lote debitado (Farmacia). Null en otros rubros.</summary>
    public Guid? StockLotId { get; init; }

    /// <summary>Número de lote (si existe en StockLots).</summary>
    public string? LotNumber { get; init; }
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

    /// <summary>0 = None, 1 = FullyReturned.</summary>
    public int ReturnStatus { get; init; }

    public SaleReturnResponse? Return { get; init; }
}
