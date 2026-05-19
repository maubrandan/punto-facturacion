using POS.Domain.Entities;

namespace POS.Application.Contracts.Fiscal;

public sealed class FiscalDocumentResponse
{
    public Guid Id { get; init; }

    public Guid SaleId { get; init; }

    public FiscalDocumentType DocumentType { get; init; }

    public FiscalDocumentStatus Status { get; init; }

    public int PointOfSale { get; init; }

    public long? VoucherNumber { get; init; }

    public string? Cae { get; init; }

    public DateTime? CaeExpiresAtUtc { get; init; }

    public string? LastErrorCode { get; init; }

    public string? LastErrorMessage { get; init; }

    public int RetryCount { get; init; }

    public DateTime? NextRetryAtUtc { get; init; }

    public string CorrelationId { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime UpdatedAtUtc { get; init; }
}
