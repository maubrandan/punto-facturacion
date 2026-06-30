using POS.Domain.Common;

namespace POS.Domain.Entities;

public enum FiscalDocumentType
{
    InvoiceA = 1,
    InvoiceB = 2,
    CreditNoteA = 3,
    CreditNoteB = 4
}

public enum FiscalDocumentStatus
{
    Draft = 0,
    PendingAuthorization = 1,
    Authorized = 2,
    Rejected = 3,
    RetryScheduled = 4,
    Cancelled = 5
}

public sealed class FiscalDocument : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public Guid SaleId { get; set; }

    public Sale? Sale { get; set; }

    public Guid? OriginalFiscalDocumentId { get; set; }

    public FiscalDocument? OriginalFiscalDocument { get; set; }

    public ICollection<FiscalDocument> CreditNotes { get; set; } = new List<FiscalDocument>();

    public FiscalDocumentType DocumentType { get; set; }

    public int PointOfSale { get; set; }

    public long? VoucherNumber { get; set; }

    public FiscalDocumentStatus Status { get; set; } = FiscalDocumentStatus.Draft;

    public string? Cae { get; set; }

    public DateTime? CaeExpiresAtUtc { get; set; }

    public DateTime? AuthorizedAtUtc { get; set; }

    public string? LastErrorCode { get; set; }

    public string? LastErrorMessage { get; set; }

    public int RetryCount { get; set; }

    public DateTime? NextRetryAtUtc { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public string? BuyerTaxId { get; set; }

    public string? BuyerName { get; set; }

    public decimal? AuthorizedAmount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsAuthorized => Status == FiscalDocumentStatus.Authorized && !string.IsNullOrWhiteSpace(Cae);

    public bool IsCreditNote =>
        DocumentType == FiscalDocumentType.CreditNoteA || DocumentType == FiscalDocumentType.CreditNoteB;

    public void MarkPending(string correlationId, DateTime nowUtc)
    {
        if (Status is FiscalDocumentStatus.Authorized or FiscalDocumentStatus.Cancelled)
            throw new InvalidOperationException("No se puede enviar un comprobante en estado final.");

        CorrelationId = correlationId;
        Status = FiscalDocumentStatus.PendingAuthorization;
        LastErrorCode = null;
        LastErrorMessage = null;
        NextRetryAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkAuthorized(long voucherNumber, string cae, DateTime caeExpiresAtUtc, DateTime nowUtc)
    {
        VoucherNumber = voucherNumber;
        Cae = cae;
        CaeExpiresAtUtc = caeExpiresAtUtc;
        AuthorizedAtUtc = nowUtc;
        Status = FiscalDocumentStatus.Authorized;
        LastErrorCode = null;
        LastErrorMessage = null;
        NextRetryAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkRejected(string errorCode, string errorMessage, DateTime nowUtc)
    {
        Status = FiscalDocumentStatus.Rejected;
        LastErrorCode = errorCode;
        LastErrorMessage = errorMessage;
        NextRetryAtUtc = null;
        UpdatedAtUtc = nowUtc;
    }

    public void ScheduleRetry(string errorCode, string errorMessage, DateTime nextRetryAtUtc, DateTime nowUtc)
    {
        RetryCount++;
        Status = FiscalDocumentStatus.RetryScheduled;
        LastErrorCode = errorCode;
        LastErrorMessage = errorMessage;
        NextRetryAtUtc = nextRetryAtUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkCancelled(DateTime nowUtc)
    {
        Status = FiscalDocumentStatus.Cancelled;
        UpdatedAtUtc = nowUtc;
    }
}
