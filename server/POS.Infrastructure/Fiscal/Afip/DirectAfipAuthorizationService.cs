using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Interfaces;
using POS.Infrastructure.Configuration;

namespace POS.Infrastructure.Fiscal.Afip;

public sealed class DirectAfipAuthorizationService
{
    private readonly AfipWsaaClient _wsaa;
    private readonly AfipWsfeClient _wsfe;
    private readonly IOptions<ArcaOptions> _options;
    private readonly ILogger<DirectAfipAuthorizationService> _logger;

    public DirectAfipAuthorizationService(
        AfipWsaaClient wsaa,
        AfipWsfeClient wsfe,
        IOptions<ArcaOptions> options,
        ILogger<DirectAfipAuthorizationService> logger)
    {
        _wsaa = wsaa;
        _wsfe = wsfe;
        _options = options;
        _logger = logger;
    }

    public async Task<FiscalAuthorizationResult> AuthorizeAsync(
        FiscalAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.CertificateRef))
            {
                return new FiscalAuthorizationResult
                {
                    IsSuccess = false,
                    IsTransientError = false,
                    ErrorCode = "afip.certificate_missing",
                    ErrorMessage = "El perfil fiscal no tiene certificado configurado."
                };
            }

            if (request.Lines.Count == 0)
            {
                return new FiscalAuthorizationResult
                {
                    IsSuccess = false,
                    IsTransientError = false,
                    ErrorCode = "afip.lines_missing",
                    ErrorMessage = "La venta no tiene líneas para autorizar en WSFE."
                };
            }

            using var certificate = FiscalCertificateLoader.Load(
                request.CertificateRef,
                request.PrivateKeyRef ?? string.Empty,
                _options);

            var (token, sign) = await _wsaa.GetCredentialsAsync(
                request.TaxId,
                certificate,
                request.IsProduction,
                cancellationToken);

            var scaledRequest = ScaleRequestToAuthorizationAmount(request);
            var (voucher, cae, expires) = await _wsfe.AuthorizeAsync(
                scaledRequest,
                token,
                sign,
                cancellationToken);

            return new FiscalAuthorizationResult
            {
                IsSuccess = true,
                VoucherNumber = voucher,
                Cae = cae,
                CaeExpiresAtUtc = expires
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            _logger.LogWarning(ex, "Error transitorio AFIP directo para {FiscalDocumentId}", request.FiscalDocumentId);
            return new FiscalAuthorizationResult
            {
                IsSuccess = false,
                IsTransientError = true,
                ErrorCode = "afip.transient",
                ErrorMessage = "No se pudo contactar AFIP/ARCA temporalmente."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error AFIP directo para {FiscalDocumentId}", request.FiscalDocumentId);
            return new FiscalAuthorizationResult
            {
                IsSuccess = false,
                IsTransientError = false,
                ErrorCode = "afip.authorization_failed",
                ErrorMessage = ex.Message
            };
        }
    }

    private static FiscalAuthorizationRequest ScaleRequestToAuthorizationAmount(FiscalAuthorizationRequest request)
    {
        var lineTotal = request.Lines.Sum(l => l.LineNetSubtotal + l.LineTaxAmount);
        if (lineTotal <= 0 || Math.Abs(lineTotal - request.TotalAmount) < 0.01m)
            return request;

        var ratio = request.TotalAmount / lineTotal;
        var scaledLines = request.Lines
            .Select(
                l => new FiscalAuthorizationLine
                {
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitNetPrice = l.UnitNetPrice,
                    TaxRate = l.TaxRate,
                    LineNetSubtotal = Math.Round(l.LineNetSubtotal * ratio, 2),
                    LineTaxAmount = Math.Round(l.LineTaxAmount * ratio, 2)
                })
            .ToList();

        return new FiscalAuthorizationRequest
        {
            TenantId = request.TenantId,
            TaxId = request.TaxId,
            PointOfSale = request.PointOfSale,
            DocumentType = request.DocumentType,
            FiscalDocumentId = request.FiscalDocumentId,
            SaleId = request.SaleId,
            TotalAmount = request.TotalAmount,
            CorrelationId = request.CorrelationId,
            BuyerTaxId = request.BuyerTaxId,
            BuyerName = request.BuyerName,
            OriginalFiscalDocumentId = request.OriginalFiscalDocumentId,
            OriginalVoucherNumber = request.OriginalVoucherNumber,
            IsProduction = request.IsProduction,
            CertificateRef = request.CertificateRef,
            PrivateKeyRef = request.PrivateKeyRef,
            Lines = scaledLines
        };
    }
}
