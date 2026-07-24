using FluentValidation;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Sales;
using POS.Application.Fiscal;
using POS.Application.Interfaces;
using POS.Application.Inventory;
using POS.Application.Sales;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Sales;

public sealed class CreateSaleReturnHandler : ICreateSaleReturnHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICashSessionService _cashSession;
    private readonly IStockPolicyFactory _policyFactory;
    private readonly IIssueCreditNoteHandler _creditNoteHandler;
    private readonly IValidator<CreateSaleReturnCommand> _validator;

    public CreateSaleReturnHandler(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        ICashSessionService cashSession,
        IStockPolicyFactory policyFactory,
        IIssueCreditNoteHandler creditNoteHandler,
        IValidator<CreateSaleReturnCommand> validator)
    {
        _db = db;
        _currentUser = currentUser;
        _cashSession = cashSession;
        _policyFactory = policyFactory;
        _creditNoteHandler = creditNoteHandler;
        _validator = validator;
    }

    public async Task<Result<SaleReturnResponse>> HandleAsync(
        CreateSaleReturnCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<SaleReturnResponse>.Failure(
                "sale_return.validation",
                string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var tenantId = _currentUser.TenantId?.Trim();
        if (string.IsNullOrEmpty(tenantId))
        {
            return Result<SaleReturnResponse>.Failure(
                "sale.tenant_required",
                "No se pudo determinar el comercio (tenant). Vuelva a autenticarse.");
        }

        var cashSessionId = await _cashSession.GetOpenSessionIdAsync(cancellationToken);
        if (cashSessionId is null)
        {
            return Result<SaleReturnResponse>.Failure(
                "cash.session_required",
                "Debe abrir la caja para registrar devoluciones. Vaya a Caja e inicie un turno.");
        }

        var sale = await _db.Sales
            .Include(s => s.Details)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == command.SaleId && s.TenantId == tenantId, cancellationToken);

        if (sale is null)
        {
            return Result<SaleReturnResponse>.Failure("sale.not_found", "Venta no encontrada.");
        }

        if (sale.ReturnStatus != SaleReturnStatus.None)
        {
            return Result<SaleReturnResponse>.Failure(
                "sale.already_returned",
                "La venta ya fue devuelta en su totalidad.");
        }

        if (sale.Details.Count == 0)
        {
            return Result<SaleReturnResponse>.Failure(
                "sale_return.empty",
                "La venta no tiene líneas para devolver.");
        }

        var policy = await _policyFactory.ForCurrentTenantAsync(cancellationToken);
        var returnId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var currentUserId = _currentUser.UserId;
        string? createdByUserId = string.IsNullOrWhiteSpace(currentUserId) ? null : currentUserId;
        var createdByUserName = createdByUserId ?? "—";
        if (!string.IsNullOrEmpty(currentUserId))
        {
            var appUser = await _db
                .Set<ApplicationUser>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.Id == currentUserId && u.TenantId == tenantId,
                    cancellationToken);
            if (appUser is not null)
            {
                createdByUserId = appUser.Id;
                createdByUserName = string.IsNullOrWhiteSpace(appUser.FullName)
                    ? (appUser.Email ?? appUser.UserName ?? createdByUserName)
                    : appUser.FullName;
            }
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var detail in sale.Details)
            {
                var product = await _db.Products
                    .FirstOrDefaultAsync(
                        p => p.Id == detail.ProductId && p.TenantId == tenantId,
                        cancellationToken);

                if (product is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<SaleReturnResponse>.Failure(
                        "sale.invalid_product",
                        "Uno o más productos de la venta ya no existen.");
                }

                var apply = new StockApplyContext
                {
                    Product = product,
                    Quantity = detail.Quantity,
                    StockLotId = detail.StockLotId,
                    ReferenceId = returnId,
                    CreatedByUserId = createdByUserId ?? currentUserId ?? string.Empty,
                    ReasonNote = "Devolución de venta"
                };

                var stockResult = await policy.ApplySaleReturnAsync(apply, cancellationToken);
                if (!stockResult.IsSuccess)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<SaleReturnResponse>.Failure(stockResult.ErrorCode!, stockResult.Error!);
                }
            }

            var saleReturn = new SaleReturn
            {
                Id = returnId,
                SaleId = sale.Id,
                ReturnedAt = now,
                CreatedByUserId = createdByUserId,
                CreatedByUserName = createdByUserName,
                CashSessionId = cashSessionId,
                TotalAmount = sale.TotalAmount
            };

            foreach (var detail in sale.Details)
            {
                saleReturn.Lines.Add(
                    new SaleReturnLine
                    {
                        Id = Guid.NewGuid(),
                        SaleDetailId = detail.Id,
                        ProductId = detail.ProductId,
                        Quantity = detail.Quantity,
                        StockLotId = detail.StockLotId,
                        LineNetSubtotal = detail.LineNetSubtotal,
                        LineTaxAmount = detail.LineTaxAmount,
                        UnitNetPrice = detail.UnitNetPrice,
                        TaxRate = detail.TaxRate,
                        ProductName = detail.ProductName,
                        ProductExtendedDataJson = detail.ProductExtendedDataJson
                    });
            }

            var creditRefunds = new List<decimal>();
            foreach (var payment in sale.Payments)
            {
                saleReturn.Payments.Add(
                    new SaleReturnPayment
                    {
                        Id = Guid.NewGuid(),
                        Method = payment.Method,
                        Amount = payment.Amount,
                        CreatedAt = now
                    });

                if (payment.Method == PaymentMethod.Credit)
                    creditRefunds.Add(payment.Amount);
            }

            if (creditRefunds.Count > 0)
            {
                if (sale.CustomerId is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<SaleReturnResponse>.Failure(
                        "sale_return.customer_required",
                        "La venta a crédito no tiene cliente asociado.");
                }

                var customerId = sale.CustomerId.Value;
                var previousBalance = await _db.CustomerAccountMovements
                    .Where(m => m.CustomerId == customerId)
                    .OrderByDescending(m => m.CreatedAt)
                    .ThenByDescending(m => m.Id)
                    .Select(m => (decimal?)m.BalanceAfter)
                    .FirstOrDefaultAsync(cancellationToken) ?? 0m;

                var runningBalance = previousBalance;
                foreach (var amount in creditRefunds)
                {
                    var signed = -decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
                    runningBalance = decimal.Round(
                        runningBalance + signed,
                        2,
                        MidpointRounding.AwayFromZero);
                    _db.CustomerAccountMovements.Add(
                        new CustomerAccountMovement
                        {
                            Id = Guid.NewGuid(),
                            CustomerId = customerId,
                            Type = CustomerAccountMovementType.Payment,
                            Amount = signed,
                            BalanceAfter = runningBalance,
                            SaleId = sale.Id,
                            Notes = "Devolución de venta (reverso cuenta corriente)",
                            SettlementMethod = null,
                            CashSessionId = cashSessionId,
                            CreatedByUserId = createdByUserId,
                            CreatedAt = now
                        });
                }
            }

            sale.ReturnStatus = SaleReturnStatus.FullyReturned;
            _db.SaleReturns.Add(saleReturn);
            await _db.SaveChangesAsync(cancellationToken);

            Guid? fiscalDocumentId = null;
            var authorizedInvoice = await _db.FiscalDocuments
                .AsNoTracking()
                .Where(
                    d => d.SaleId == sale.Id
                        && d.TenantId == tenantId
                        && d.Status == FiscalDocumentStatus.Authorized
                        && (d.DocumentType == FiscalDocumentType.InvoiceA
                            || d.DocumentType == FiscalDocumentType.InvoiceB)
                        && d.Cae != null
                        && d.Cae != "")
                .OrderByDescending(d => d.AuthorizedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (authorizedInvoice is not null)
            {
                var targetNcType = authorizedInvoice.DocumentType == FiscalDocumentType.InvoiceA
                    ? FiscalDocumentType.CreditNoteA
                    : FiscalDocumentType.CreditNoteB;

                var existingNc = await _db.FiscalDocuments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        d => d.TenantId == tenantId
                            && d.SaleId == sale.Id
                            && d.OriginalFiscalDocumentId == authorizedInvoice.Id
                            && d.DocumentType == targetNcType
                            && d.Status == FiscalDocumentStatus.Authorized,
                        cancellationToken);

                if (existingNc is not null)
                {
                    fiscalDocumentId = existingNc.Id;
                }
                else
                {
                    var ncResult = await _creditNoteHandler.HandleAsync(
                        new IssueCreditNoteCommand(
                            authorizedInvoice.Id,
                            sale.Id,
                            sale.TotalAmount),
                        cancellationToken);

                    if (!ncResult.IsSuccess)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<SaleReturnResponse>.Failure(ncResult.ErrorCode!, ncResult.Error!);
                    }

                    fiscalDocumentId = ncResult.Value!.Id;
                }

                if (fiscalDocumentId is not null)
                {
                    saleReturn.FiscalDocumentId = fiscalDocumentId;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);

            return Result<SaleReturnResponse>.Ok(MapResponse(saleReturn));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static SaleReturnResponse MapResponse(SaleReturn saleReturn) =>
        new()
        {
            Id = saleReturn.Id,
            SaleId = saleReturn.SaleId,
            ReturnedAt = saleReturn.ReturnedAt,
            TotalAmount = saleReturn.TotalAmount,
            CreatedByUserName = saleReturn.CreatedByUserName,
            CashSessionId = saleReturn.CashSessionId,
            FiscalDocumentId = saleReturn.FiscalDocumentId,
            Lines = saleReturn.Lines
                .Select(
                    l => new SaleReturnLineResponse
                    {
                        Id = l.Id,
                        SaleDetailId = l.SaleDetailId,
                        ProductId = l.ProductId,
                        ProductName = l.ProductName,
                        Quantity = l.Quantity,
                        StockLotId = l.StockLotId,
                        LineNetSubtotal = l.LineNetSubtotal,
                        LineTaxAmount = l.LineTaxAmount
                    })
                .ToList(),
            Payments = saleReturn.Payments
                .Select(
                    p => new SalePaymentResponse
                    {
                        Id = p.Id,
                        Method = (int)p.Method,
                        Amount = p.Amount
                    })
                .ToList()
        };
}
