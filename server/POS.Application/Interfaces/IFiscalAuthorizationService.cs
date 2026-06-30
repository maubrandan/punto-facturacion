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

    public string? BuyerTaxId { get; init; }

    public string? BuyerName { get; init; }

    public Guid? OriginalFiscalDocumentId { get; init; }

    public long? OriginalVoucherNumber { get; init; }

    public bool IsProduction { get; init; }

    public string? CertificateRef { get; init; }

    public string? PrivateKeyRef { get; init; }

    public IReadOnlyList<FiscalAuthorizationLine> Lines { get; init; } = Array.Empty<FiscalAuthorizationLine>();
}

public sealed class FiscalAuthorizationLine
{
    public required string Description { get; init; }

    public required int Quantity { get; init; }

    public required decimal UnitNetPrice { get; init; }

    public required decimal TaxRate { get; init; }

    public required decimal LineNetSubtotal { get; init; }

    public required decimal LineTaxAmount { get; init; }
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
