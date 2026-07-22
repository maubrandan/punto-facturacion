namespace POS.Application.Contracts.Fiscal;

public sealed class IssueElectronicInvoiceRequest
{
    public Guid SaleId { get; init; }

    public bool IsInvoiceA { get; init; }

    public string? BuyerTaxId { get; init; }

    public string? BuyerName { get; init; }

    /// <summary>Si se informa, completa CUIT/razón social desde el maestro de clientes del tenant.</summary>
    public Guid? CustomerId { get; init; }
}

public sealed class RetryElectronicInvoiceRequest
{
    public Guid FiscalDocumentId { get; init; }
}

public sealed class IssueCreditNoteRequest
{
    public Guid OriginalFiscalDocumentId { get; init; }

    public Guid SaleId { get; init; }

    public decimal Amount { get; init; }
}
