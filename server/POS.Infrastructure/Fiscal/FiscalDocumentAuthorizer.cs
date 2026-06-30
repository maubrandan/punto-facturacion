using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Common;
using POS.Application.Contracts.Fiscal;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Configuration;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Fiscal;

internal sealed class FiscalDocumentAuthorizer
{
    private readonly ApplicationDbContext _db;
    private readonly IFiscalAuthorizationService _fiscalAuthorization;
    private readonly IOptions<ArcaOptions> _arcaOptions;
    private readonly ILogger _logger;

    public FiscalDocumentAuthorizer(
        ApplicationDbContext db,
        IFiscalAuthorizationService fiscalAuthorization,
        IOptions<ArcaOptions> arcaOptions,
        ILogger logger)
    {
        _db = db;
        _fiscalAuthorization = fiscalAuthorization;
        _arcaOptions = arcaOptions;
        _logger = logger;
    }

    public async Task<Result<FiscalDocumentResponse>> AuthorizeAsync(
        FiscalDocument document,
        TenantFiscalProfile profile,
        Sale sale,
        decimal authorizationAmount,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");
        document.MarkPending(correlationId, now);
        document.AuthorizedAmount = authorizationAmount;

        long? originalVoucher = null;
        if (document.OriginalFiscalDocumentId.HasValue)
        {
            var original = await _db.Set<FiscalDocument>()
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == document.OriginalFiscalDocumentId.Value, cancellationToken);
            originalVoucher = original?.VoucherNumber;
        }

        var lines = sale.Details
            .OrderBy(d => d.Id)
            .Select(
                d => new FiscalAuthorizationLine
                {
                    Description = d.ProductName,
                    Quantity = d.Quantity,
                    UnitNetPrice = d.UnitNetPrice,
                    TaxRate = d.TaxRate,
                    LineNetSubtotal = d.LineNetSubtotal,
                    LineTaxAmount = d.LineTaxAmount
                })
            .ToList();

        var authResult = await _fiscalAuthorization.AuthorizeAsync(
            new FiscalAuthorizationRequest
            {
                TenantId = document.TenantId,
                TaxId = profile.TaxId,
                PointOfSale = document.PointOfSale,
                DocumentType = document.DocumentType,
                FiscalDocumentId = document.Id,
                SaleId = document.SaleId,
                TotalAmount = authorizationAmount,
                CorrelationId = correlationId,
                BuyerTaxId = document.BuyerTaxId,
                BuyerName = document.BuyerName,
                OriginalFiscalDocumentId = document.OriginalFiscalDocumentId,
                OriginalVoucherNumber = originalVoucher,
                IsProduction = profile.IsProduction,
                CertificateRef = profile.CertificateRef,
                PrivateKeyRef = profile.PrivateKeyRef,
                Lines = lines
            },
            cancellationToken);

        if (authResult.IsSuccess && authResult.VoucherNumber.HasValue && !string.IsNullOrWhiteSpace(authResult.Cae))
        {
            document.MarkAuthorized(
                authResult.VoucherNumber.Value,
                authResult.Cae!,
                authResult.CaeExpiresAtUtc ?? now.AddDays(10),
                now);
            await _db.SaveChangesAsync(cancellationToken);
            return Result<FiscalDocumentResponse>.Ok(
                FiscalDocumentMapper.ToResponse(document, profile.TaxId));
        }

        if (authResult.IsTransientError && document.RetryCount < _arcaOptions.Value.RetryMaxAttempts)
        {
            var nextRetry = now.AddSeconds(
                Math.Min(
                    _arcaOptions.Value.RetryMaxDelayMinutes * 60,
                    _arcaOptions.Value.RetryBaseDelaySeconds * (int)Math.Pow(2, document.RetryCount)));
            document.ScheduleRetry(
                authResult.ErrorCode ?? "fiscal.transient",
                authResult.ErrorMessage ?? "Error transitorio de autorización.",
                nextRetry,
                now);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                "Comprobante {FiscalDocumentId} agendado para reintento en {NextRetryAtUtc}",
                document.Id,
                nextRetry);
            return Result<FiscalDocumentResponse>.Failure(
                "fiscal.retry_scheduled",
                "ARCA no respondió de forma estable. Se programó reintento automático.");
        }

        document.MarkRejected(
            authResult.ErrorCode ?? "fiscal.authorization_failed",
            authResult.ErrorMessage ?? "ARCA rechazó la autorización.",
            now);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<FiscalDocumentResponse>.Failure(
            document.LastErrorCode ?? "fiscal.authorization_failed",
            document.LastErrorMessage ?? "ARCA rechazó la autorización.");
    }
}
