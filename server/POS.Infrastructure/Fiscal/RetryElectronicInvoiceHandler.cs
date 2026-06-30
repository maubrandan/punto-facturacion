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

public sealed class RetryElectronicInvoiceHandler : IRetryElectronicInvoiceHandler
{
    private readonly ApplicationDbContext _db;
    private readonly IValidator<RetryElectronicInvoiceCommand> _validator;
    private readonly IFiscalAuthorizationService _fiscalAuthorization;
    private readonly ICurrentUserService _currentUser;
    private readonly IOptions<ArcaOptions> _arcaOptions;
    private readonly ILogger<RetryElectronicInvoiceHandler> _logger;

    public RetryElectronicInvoiceHandler(
        ApplicationDbContext db,
        IValidator<RetryElectronicInvoiceCommand> validator,
        IFiscalAuthorizationService fiscalAuthorization,
        ICurrentUserService currentUser,
        IOptions<ArcaOptions> arcaOptions,
        ILogger<RetryElectronicInvoiceHandler> logger)
    {
        _db = db;
        _validator = validator;
        _fiscalAuthorization = fiscalAuthorization;
        _currentUser = currentUser;
        _arcaOptions = arcaOptions;
        _logger = logger;
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

        var profile = await _db.Set<TenantFiscalProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.IsEnabled, cancellationToken);
        if (profile is null)
            return Result<FiscalDocumentResponse>.Failure("fiscal.profile_missing", "Falta perfil fiscal habilitado.");

        if (document.IsAuthorized)
            return Result<FiscalDocumentResponse>.Ok(FiscalDocumentMapper.ToResponse(document, profile.TaxId));

        var sale = await _db.Sales
            .Include(s => s.Details)
            .FirstOrDefaultAsync(s => s.Id == document.SaleId && s.TenantId == tenantId, cancellationToken);
        if (sale is null)
            return Result<FiscalDocumentResponse>.Failure("fiscal.sale_not_found", "Venta asociada no encontrada.");

        var amount = document.AuthorizedAmount ?? sale.TotalAmount;
        var authorizer = new FiscalDocumentAuthorizer(
            _db,
            _fiscalAuthorization,
            _arcaOptions,
            _logger);

        return await authorizer.AuthorizeAsync(
            document,
            profile,
            sale,
            amount,
            cancellationToken);
    }
}
