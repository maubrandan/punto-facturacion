using POS.Domain.Entities;

namespace POS.Application.Interfaces;

public sealed class FiscalAuthorizationRequest
{
    public required string TenantId { get; init; }

    public required string TaxId { get; init; }

    public required int PointOfSale { get; init; }

    public required FiscalDocumentType DocumentType { get; init; }

    public required Guid FiscalDocumentId { get; init; }

    public required Guid SaleId { get; init; }

    public required decimal TotalAmount { get; init; }

    public required string CorrelationId { get; init; }
}

public sealed class FiscalAuthorizationResult
{
    public bool IsSuccess { get; init; }

    public bool IsTransientError { get; init; }

    public long? VoucherNumber { get; init; }

    public string? Cae { get; init; }

    public DateTime? CaeExpiresAtUtc { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}

public interface IFiscalAuthorizationService
{
    Task<FiscalAuthorizationResult> AuthorizeAsync(
        FiscalAuthorizationRequest request,
        CancellationToken cancellationToken = default);
}
