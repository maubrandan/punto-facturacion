using FluentValidation;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Fiscal;
using POS.Application.Fiscal;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Fiscal;

public sealed class RetryElectronicInvoiceHandler : IRetryElectronicInvoiceHandler
{
    private readonly ApplicationDbContext _db;
    private readonly IValidator<RetryElectronicInvoiceCommand> _validator;
    private readonly IFiscalAuthorizationService _fiscalAuthorization;
    private readonly ICurrentUserService _currentUser;

    public RetryElectronicInvoiceHandler(
        ApplicationDbContext db,
        IValidator<RetryElectronicInvoiceCommand> validator,
        IFiscalAuthorizationService fiscalAuthorization,
        ICurrentUserService currentUser)
    {
        _db = db;
        _validator = validator;
        _fiscalAuthorization = fiscalAuthorization;
        _currentUser = currentUser;
    }

    public async Task<Result<FiscalDocumentResponse>> HandleAsync(
        RetryElectronicInvoiceCommand command,
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

        var document = await _db.Set<FiscalDocument>()
            .FirstOrDefaultAsync(d => d.Id == command.FiscalDocumentId && d.TenantId == tenantId, cancellationToken);
        if (document is null)
            return Result<FiscalDocumentResponse>.Failure("fiscal.not_found", "Comprobante fiscal no encontrado.");

        if (document.IsAuthorized)
            return Result<FiscalDocumentResponse>.Ok(FiscalDocumentMapper.ToResponse(document));

        var profile = await _db.Set<TenantFiscalProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.IsEnabled, cancellationToken);
        if (profile is null)
            return Result<FiscalDocumentResponse>.Failure("fiscal.profile_missing", "Falta perfil fiscal habilitado.");

        var sale = await _db.Sales
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == document.SaleId && s.TenantId == tenantId, cancellationToken);
        if (sale is null)
            return Result<FiscalDocumentResponse>.Failure("fiscal.sale_not_found", "Venta asociada no encontrada.");

        var now = DateTime.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");
        document.MarkPending(correlationId, now);
        var authResult = await _fiscalAuthorization.AuthorizeAsync(
            new FiscalAuthorizationRequest
            {
                TenantId = tenantId,
                TaxId = profile.TaxId,
                PointOfSale = document.PointOfSale,
                DocumentType = document.DocumentType,
                FiscalDocumentId = document.Id,
                SaleId = document.SaleId,
                TotalAmount = sale.TotalAmount,
                CorrelationId = correlationId
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
            return Result<FiscalDocumentResponse>.Ok(FiscalDocumentMapper.ToResponse(document));
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
