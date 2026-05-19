using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Interfaces;
using POS.Infrastructure.Configuration;

namespace POS.Infrastructure.Fiscal;

public sealed class ArcaFiscalAuthorizationService : IFiscalAuthorizationService
{
    private readonly IOptions<ArcaOptions> _options;
    private readonly ILogger<ArcaFiscalAuthorizationService> _logger;

    public ArcaFiscalAuthorizationService(
        IOptions<ArcaOptions> options,
        ILogger<ArcaFiscalAuthorizationService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<FiscalAuthorizationResult> AuthorizeAsync(
        FiscalAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (options.SandboxAutoApprove)
        {
            var voucher = Math.Abs(request.FiscalDocumentId.GetHashCode());
            var cae = $"{voucher:00000000000000}";
            return new FiscalAuthorizationResult
            {
                IsSuccess = true,
                VoucherNumber = voucher,
                Cae = cae,
                CaeExpiresAtUtc = DateTime.UtcNow.AddDays(10)
            };
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var payload = new
            {
                request.TenantId,
                request.TaxId,
                request.PointOfSale,
                DocumentType = request.DocumentType.ToString(),
                request.SaleId,
                request.FiscalDocumentId,
                request.TotalAmount,
                request.CorrelationId
            };

            using var response = await client.PostAsJsonAsync(
                options.AuthorizationEndpoint,
                payload,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new FiscalAuthorizationResult
                {
                    IsSuccess = false,
                    IsTransientError = (int)response.StatusCode >= 500,
                    ErrorCode = "arca.http_error",
                    ErrorMessage = $"ARCA devolvió {(int)response.StatusCode}."
                };
            }

            var body = await response.Content.ReadFromJsonAsync<ArcaResponse>(cancellationToken: cancellationToken);
            if (body is null || !body.success)
            {
                return new FiscalAuthorizationResult
                {
                    IsSuccess = false,
                    IsTransientError = false,
                    ErrorCode = body?.errorCode ?? "arca.invalid_response",
                    ErrorMessage = body?.errorMessage ?? "Respuesta inválida de ARCA."
                };
            }

            return new FiscalAuthorizationResult
            {
                IsSuccess = true,
                VoucherNumber = body.voucherNumber,
                Cae = body.cae,
                CaeExpiresAtUtc = body.caeExpiresAtUtc
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error transitorio consultando ARCA para fiscalDocumentId {FiscalDocumentId}", request.FiscalDocumentId);
            return new FiscalAuthorizationResult
            {
                IsSuccess = false,
                IsTransientError = true,
                ErrorCode = "arca.transient",
                ErrorMessage = "No se pudo contactar ARCA temporalmente."
            };
        }
    }

    private sealed class ArcaResponse
    {
        public bool success { get; set; }
        public long voucherNumber { get; set; }
        public string cae { get; set; } = string.Empty;
        public DateTime caeExpiresAtUtc { get; set; }
        public string? errorCode { get; set; }
        public string? errorMessage { get; set; }
    }
}
