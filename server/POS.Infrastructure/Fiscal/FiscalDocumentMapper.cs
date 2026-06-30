using POS.Application.Contracts.Fiscal;
using POS.Domain.Billing.Afip;
using POS.Domain.Entities;

namespace POS.Infrastructure.Fiscal;

internal static class FiscalDocumentMapper
{
    public static FiscalDocumentResponse ToResponse(FiscalDocument document, string? sellerTaxId = null)
    {
        var amount = document.AuthorizedAmount ?? 0m;
        string? qrUrl = null;
        if (document.IsAuthorized
            && document.VoucherNumber.HasValue
            && !string.IsNullOrWhiteSpace(document.Cae)
            && !string.IsNullOrWhiteSpace(sellerTaxId))
        {
            qrUrl = AfipQrCodeBuilder.BuildUrl(
                sellerTaxId,
                document.PointOfSale,
                AfipComprobanteTypes.ToAfipCode(document.DocumentType),
                document.VoucherNumber.Value,
                amount,
                document.Cae,
                document.AuthorizedAtUtc ?? document.CreatedAtUtc,
                document.BuyerTaxId);
        }

        return new FiscalDocumentResponse
        {
            Id = document.Id,
            SaleId = document.SaleId,
            OriginalFiscalDocumentId = document.OriginalFiscalDocumentId,
            DocumentType = document.DocumentType,
            Status = document.Status,
            PointOfSale = document.PointOfSale,
            VoucherNumber = document.VoucherNumber,
            Cae = document.Cae,
            CaeExpiresAtUtc = document.CaeExpiresAtUtc,
            LastErrorCode = document.LastErrorCode,
            LastErrorMessage = document.LastErrorMessage,
            RetryCount = document.RetryCount,
            NextRetryAtUtc = document.NextRetryAtUtc,
            CorrelationId = document.CorrelationId,
            CreatedAtUtc = document.CreatedAtUtc,
            UpdatedAtUtc = document.UpdatedAtUtc,
            BuyerTaxId = document.BuyerTaxId,
            BuyerName = document.BuyerName,
            AuthorizedAmount = document.AuthorizedAmount,
            DocumentTypeLabel = AfipComprobanteTypes.ToDisplayLabel(document.DocumentType),
            AfipQrUrl = qrUrl
        };
    }
}
