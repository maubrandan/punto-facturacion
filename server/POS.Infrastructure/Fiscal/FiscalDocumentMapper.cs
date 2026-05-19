using POS.Application.Contracts.Fiscal;
using POS.Domain.Entities;

namespace POS.Infrastructure.Fiscal;

internal static class FiscalDocumentMapper
{
    public static FiscalDocumentResponse ToResponse(FiscalDocument document) =>
        new()
        {
            Id = document.Id,
            SaleId = document.SaleId,
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
            UpdatedAtUtc = document.UpdatedAtUtc
        };
}
