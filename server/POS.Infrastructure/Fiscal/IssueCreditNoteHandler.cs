using FluentValidation;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Fiscal;
using POS.Application.Fiscal;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Fiscal;

public sealed class IssueCreditNoteHandler : IIssueCreditNoteHandler
{
    private readonly ApplicationDbContext _db;
    private readonly IValidator<IssueCreditNoteCommand> _validator;
    private readonly ICurrentUserService _currentUser;

    public IssueCreditNoteHandler(
        ApplicationDbContext db,
        IValidator<IssueCreditNoteCommand> validator,
        ICurrentUserService currentUser)
    {
        _db = db;
        _validator = validator;
        _currentUser = currentUser;
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
            .AsNoTracking()
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
            return Result<FiscalDocumentResponse>.Ok(FiscalDocumentMapper.ToResponse(existing));
        }

        if (command.Amount > sale.TotalAmount)
        {
            return Result<FiscalDocumentResponse>.Failure(
                "fiscal.credit_amount_invalid",
                "El importe de la nota de crédito no puede superar el total de la venta.");
        }

        var now = DateTime.UtcNow;
        var credit = new FiscalDocument
        {
            Id = Guid.NewGuid(),
            SaleId = command.SaleId,
            OriginalFiscalDocumentId = original.Id,
            DocumentType = targetType,
            PointOfSale = original.PointOfSale,
            Status = FiscalDocumentStatus.Authorized,
            VoucherNumber = original.VoucherNumber + 1,
            Cae = original.Cae,
            CaeExpiresAtUtc = original.CaeExpiresAtUtc,
            AuthorizedAtUtc = now,
            CorrelationId = $"NC-{Guid.NewGuid():N}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.Set<FiscalDocument>().Add(credit);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<FiscalDocumentResponse>.Ok(FiscalDocumentMapper.ToResponse(credit));
    }
}
