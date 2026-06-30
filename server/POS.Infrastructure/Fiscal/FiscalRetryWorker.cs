using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Configuration;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Fiscal;

public sealed class FiscalRetryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ArcaOptions> _options;
    private readonly ILogger<FiscalRetryWorker> _logger;

    public FiscalRetryWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ArcaOptions> options,
        ILogger<FiscalRetryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingRetriesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando reintentos fiscales.");
            }

            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }

    private async Task ProcessPendingRetriesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var fiscal = scope.ServiceProvider.GetRequiredService<IFiscalAuthorizationService>();
        var now = DateTime.UtcNow;

        var pending = await db.Set<FiscalDocument>()
            .Where(d => d.Status == FiscalDocumentStatus.RetryScheduled && d.NextRetryAtUtc <= now)
            .OrderBy(d => d.NextRetryAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var document in pending)
        {
            var profile = await db.Set<TenantFiscalProfile>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.TenantId == document.TenantId && p.IsEnabled, cancellationToken);
            var sale = await db.Sales
                .AsNoTracking()
                .Include(s => s.Details)
                .FirstOrDefaultAsync(s => s.Id == document.SaleId && s.TenantId == document.TenantId, cancellationToken);
            if (profile is null || sale is null)
            {
                document.MarkRejected("fiscal.retry_context_missing", "No se encontró contexto para reintentar.", now);
                continue;
            }

            var correlationId = Guid.NewGuid().ToString("N");
            document.MarkPending(correlationId, now);
            var amount = document.AuthorizedAmount ?? sale.TotalAmount;
            long? originalVoucher = null;
            if (document.OriginalFiscalDocumentId.HasValue)
            {
                var original = await db.Set<FiscalDocument>()
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

            var result = await fiscal.AuthorizeAsync(
                new FiscalAuthorizationRequest
                {
                    TenantId = document.TenantId,
                    TaxId = profile.TaxId,
                    PointOfSale = document.PointOfSale,
                    DocumentType = document.DocumentType,
                    FiscalDocumentId = document.Id,
                    SaleId = document.SaleId,
                    TotalAmount = amount,
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

            if (result.IsSuccess && result.VoucherNumber.HasValue && !string.IsNullOrWhiteSpace(result.Cae))
            {
                document.MarkAuthorized(
                    result.VoucherNumber.Value,
                    result.Cae!,
                    result.CaeExpiresAtUtc ?? now.AddDays(10),
                    now);
                continue;
            }

            if (result.IsTransientError && document.RetryCount < _options.Value.RetryMaxAttempts)
            {
                var nextRetry = now.AddSeconds(
                    Math.Min(
                        _options.Value.RetryMaxDelayMinutes * 60,
                        _options.Value.RetryBaseDelaySeconds * (int)Math.Pow(2, document.RetryCount)));
                document.ScheduleRetry(
                    result.ErrorCode ?? "fiscal.retry_transient",
                    result.ErrorMessage ?? "Error transitorio reintentando autorización fiscal.",
                    nextRetry,
                    now);
                continue;
            }

            document.MarkRejected(
                result.ErrorCode ?? "fiscal.retry_failed",
                result.ErrorMessage ?? "No se pudo autorizar el comprobante.",
                now);
        }

        if (pending.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }
}
