namespace POS.Application.Fiscal;

public sealed record IssueElectronicInvoiceCommand(
    Guid SaleId,
    bool IsInvoiceA,
    string? BuyerTaxId,
    string? BuyerName);

public sealed record RetryElectronicInvoiceCommand(Guid FiscalDocumentId);

public sealed record IssueCreditNoteCommand(Guid OriginalFiscalDocumentId, Guid SaleId, decimal Amount);
