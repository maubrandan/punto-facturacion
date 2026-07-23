namespace POS.Application.Contracts.Sales;

public sealed class SaleLineResponse
{
    public Guid Id { get; init; }

    public Guid ProductId { get; init; }

    public decimal Quantity { get; init; }

    public decimal LineNetSubtotal { get; init; }

    public decimal LineTaxAmount { get; init; }

    public decimal UnitNetPrice { get; init; }

    public decimal TaxRate { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string ProductExtendedDataJson { get; init; } = "{}";

    /// <summary>
    /// Lote debitado (Farmacia). En FEFO multi-lote la venta puede devolver
    /// una línea de respuesta por cada lote consumido (mismo producto).
    /// </summary>
    public Guid? StockLotId { get; init; }

    /// <summary>Número de lote aplicado (Farmacia), si corresponde.</summary>
    public string? LotNumber { get; init; }
}

public sealed class SalePaymentResponse
{
    public Guid Id { get; init; }

    public int Method { get; init; }

    public decimal Amount { get; init; }
}

public sealed class SaleResponse
{
    public Guid Id { get; init; }

    public DateTime Date { get; init; }

    public decimal TotalNet { get; init; }

    public decimal TotalTax { get; init; }

    public decimal TotalAmount { get; init; }

    public Guid? CustomerId { get; init; }

    public IReadOnlyList<SaleLineResponse> Lines { get; init; } = Array.Empty<SaleLineResponse>();

    public IReadOnlyList<SalePaymentResponse> Payments { get; init; } = Array.Empty<SalePaymentResponse>();
}
