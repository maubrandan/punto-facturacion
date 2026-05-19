namespace POS.Application.Contracts.Fiscal;

public sealed class IssueElectronicInvoiceRequest
{
    public Guid SaleId { get; init; }

    public bool IsInvoiceA { get; init; } = true;
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
