using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Application.Common;
using POS.Application.Contracts.Fiscal;
using POS.Application.Fiscal;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Configuration;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Fiscal;

public sealed class IssueCreditNoteHandler : IIssueCreditNoteHandler
{
    private readonly ApplicationDbContext _db;
    private readonly IValidator<IssueCreditNoteCommand> _validator;
    private readonly IFiscalAuthorizationService _fiscalAuthorization;
    private readonly ICurrentUserService _currentUser;
    private readonly IOptions<ArcaOptions> _arcaOptions;
    private readonly ILogger<IssueCreditNoteHandler> _logger;

    public IssueCreditNoteHandler(
        ApplicationDbContext db,
        IValidator<IssueCreditNoteCommand> validator,
        IFiscalAuthorizationService fiscalAuthorization,
        ICurrentUserService currentUser,
        IOptions<ArcaOptions> arcaOptions,
        ILogger<IssueCreditNoteHandler> logger)
    {
        _db = db;
        _validator = validator;
        _fiscalAuthorization = fiscalAuthorization;
        _currentUser = currentUser;
        _arcaOptions = arcaOptions;
        _logger = logger;
    }

    public async Task<Result<FiscalDocumentResponse>> HandleAsync(
        IssueCreditNoteCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<FiscalDocumentResponse>.Failure(
                "fiscal.validation",
                string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var tenantId = _currentUser.TenantId?.Trim();
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<FiscalDocumentResponse>.Failure("fiscal.tenant_required", "No se pudo resolver el tenant actual.");

        var original = await _db.Set<FiscalDocument>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == command.OriginalFiscalDocumentId && d.TenantId == tenantId, cancellationToken);
        if (original is null || !original.IsAuthorized)
        {
            return Result<FiscalDocumentResponse>.Failure(
                "fiscal.original_not_authorized",
                "La nota de crédito requiere comprobante origen autorizado.");
        }

        if (original.VoucherNumber is null)
        {
            return Result<FiscalDocumentResponse>.Failure(
                "fiscal.original_missing_voucher",
                "El comprobante origen no tiene numeración fiscal válida.");
        }

        var sale = await _db.Sales
            .Include(s => s.Details)
            .FirstOrDefaultAsync(s => s.Id == command.SaleId && s.TenantId == tenantId, cancellationToken);
        if (sale is null)
        {
            return Result<FiscalDocumentResponse>.Failure("fiscal.sale_not_found", "Venta asociada no encontrada.");
        }

        var targetType = original.DocumentType == FiscalDocumentType.InvoiceA
            ? FiscalDocumentType.CreditNoteA
            : FiscalDocumentType.CreditNoteB;

        var existing = await _db.Set<FiscalDocument>()
            .FirstOrDefaultAsync(
                d => d.TenantId == tenantId
                    && d.SaleId == command.SaleId
                    && d.OriginalFiscalDocumentId == original.Id
                    && d.DocumentType == targetType,
                cancellationToken);
        if (existing is not null)
        {
            var profileTaxId = await GetProfileTaxIdAsync(tenantId, cancellationToken);
            if (existing.IsAuthorized)
                return Result<FiscalDocumentResponse>.Ok(FiscalDocumentMapper.ToResponse(existing, profileTaxId));

            if (existing.Status is FiscalDocumentStatus.PendingAuthorization)
            {
                return Result<FiscalDocumentResponse>.Failure(
                    "fiscal.pending",
                    "La nota de crédito ya se encuentra en proceso de autorización.");
            }
        }

        if (command.Amount > sale.TotalAmount)
        {
            return Result<FiscalDocumentResponse>.Failure(
                "fiscal.credit_amount_invalid",
                "El importe de la nota de crédito no puede superar el total de la venta.");
        }

        var profile = await _db.Set<TenantFiscalProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.IsEnabled, cancellationToken);
        if (profile is null)
        {
            return Result<FiscalDocumentResponse>.Failure(
                "fiscal.profile_missing",
                "El tenant no tiene perfil fiscal ARCA configurado.");
        }

        var now = DateTime.UtcNow;
        var credit = existing ?? new FiscalDocument
        {
            Id = Guid.NewGuid(),
            SaleId = command.SaleId,
            OriginalFiscalDocumentId = original.Id,
            DocumentType = targetType,
            PointOfSale = original.PointOfSale,
            Status = FiscalDocumentStatus.Draft,
            BuyerTaxId = original.BuyerTaxId,
            BuyerName = original.BuyerName,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        if (existing is null)
            _db.Set<FiscalDocument>().Add(credit);

        var authorizer = new FiscalDocumentAuthorizer(
            _db,
            _fiscalAuthorization,
            _arcaOptions,
            _logger);

        return await authorizer.AuthorizeAsync(
            credit,
            profile,
            sale,
            command.Amount,
            cancellationToken);
    }

    private async Task<string?> GetProfileTaxIdAsync(string tenantId, CancellationToken cancellationToken) =>
        await _db.Set<TenantFiscalProfile>()
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.TaxId)
            .FirstOrDefaultAsync(cancellationToken);
}
