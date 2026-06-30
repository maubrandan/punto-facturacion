using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS.Infrastructure.Configuration;
using POS.Application.Common;
using POS.Application.Contracts.Fiscal;
using POS.Application.Fiscal;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Fiscal;

public sealed class IssueElectronicInvoiceHandler : IIssueElectronicInvoiceHandler
{
    private readonly ApplicationDbContext _db;
    private readonly IValidator<IssueElectronicInvoiceCommand> _validator;
    private readonly IFiscalAuthorizationService _fiscalAuthorization;
    private readonly ICurrentUserService _currentUser;
    private readonly IOptions<ArcaOptions> _arcaOptions;
    private readonly ILogger<IssueElectronicInvoiceHandler> _logger;

    public IssueElectronicInvoiceHandler(
        ApplicationDbContext db,
        IValidator<IssueElectronicInvoiceCommand> validator,
        IFiscalAuthorizationService fiscalAuthorization,
        ICurrentUserService currentUser,
        IOptions<ArcaOptions> arcaOptions,
        ILogger<IssueElectronicInvoiceHandler> logger)
    {
        _db = db;
        _validator = validator;
        _fiscalAuthorization = fiscalAuthorization;
        _currentUser = currentUser;
        _arcaOptions = arcaOptions;
        _logger = logger;
    }

    public async Task<Result<FiscalDocumentResponse>> HandleAsync(
        IssueElectronicInvoiceCommand command,
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
        {
            return Result<FiscalDocumentResponse>.Failure("fiscal.tenant_required", "No se pudo resolver el tenant actual.");
        }

        var sale = await _db.Sales
            .Include(s => s.Details)
            .FirstOrDefaultAsync(s => s.Id == command.SaleId && s.TenantId == tenantId, cancellationToken);
        if (sale is null)
        {
            return Result<FiscalDocumentResponse>.Failure("fiscal.sale_not_found", "La venta no existe para este tenant.");
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

        var docType = command.IsInvoiceA ? FiscalDocumentType.InvoiceA : FiscalDocumentType.InvoiceB;
        var existing = await _db.Set<FiscalDocument>()
            .FirstOrDefaultAsync(
                d => d.TenantId == tenantId && d.SaleId == command.SaleId && d.DocumentType == docType,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.IsAuthorized)
                return Result<FiscalDocumentResponse>.Ok(FiscalDocumentMapper.ToResponse(existing, profile.TaxId));

            if (existing.Status is FiscalDocumentStatus.PendingAuthorization)
            {
                return Result<FiscalDocumentResponse>.Failure(
                    "fiscal.pending",
                    "El comprobante ya se encuentra en proceso de autorización.");
            }
        }

        var now = DateTime.UtcNow;
        var buyerTaxId = NormalizeOptional(command.BuyerTaxId);
        var buyerName = command.BuyerName?.Trim();
        var document = existing ?? new FiscalDocument
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            DocumentType = docType,
            PointOfSale = profile.PointOfSale,
            Status = FiscalDocumentStatus.Draft,
            BuyerTaxId = buyerTaxId,
            BuyerName = buyerName,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        if (existing is null)
            _db.Set<FiscalDocument>().Add(document);
        else
        {
            document.BuyerTaxId = buyerTaxId;
            document.BuyerName = buyerName;
        }

        var authorizer = new FiscalDocumentAuthorizer(
            _db,
            _fiscalAuthorization,
            _arcaOptions,
            _logger);

        return await authorizer.AuthorizeAsync(
            document,
            profile,
            sale,
            sale.TotalAmount,
            cancellationToken);
    }

    private static string? NormalizeOptional(string? taxId)
    {
        if (string.IsNullOrWhiteSpace(taxId))
            return null;
        return new string(taxId.Where(char.IsDigit).ToArray());
    }
}
