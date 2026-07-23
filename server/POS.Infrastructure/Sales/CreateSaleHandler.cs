using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Sales;
using POS.Application.Interfaces;
using POS.Application.Inventory;
using POS.Application.Sales;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Sales;

public sealed class CreateSaleHandler : ICreateSaleHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICashSessionService _cashSession;
    private readonly ITenantEntitlementGuard _entitlements;
    private readonly IStockPolicyFactory _policyFactory;

    public CreateSaleHandler(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        ICashSessionService cashSession,
        ITenantEntitlementGuard entitlements,
        IStockPolicyFactory policyFactory)
    {
        _db = db;
        _currentUser = currentUser;
        _cashSession = cashSession;
        _entitlements = entitlements;
        _policyFactory = policyFactory;
    }

    public async Task<Result<SaleResponse>> HandleAsync(
        CreateSaleCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Lines.Count == 0)
        {
            return Result<SaleResponse>.Failure("sale.validation", "Debe haber al menos una línea de venta.");
        }

        if (command.Payments.Count == 0)
        {
            return Result<SaleResponse>.Failure(
                "sale.payment_required",
                "Debe indicar al menos un medio de pago.");
        }

        foreach (var payment in command.Payments)
        {
            if (payment.Amount <= 0m)
            {
                return Result<SaleResponse>.Failure(
                    "sale.payment_invalid",
                    "Cada cobro debe tener un monto mayor a cero.");
            }

            if (!Enum.IsDefined(typeof(PaymentMethod), payment.Method))
            {
                return Result<SaleResponse>.Failure(
                    "sale.payment_invalid",
                    "Medio de pago no válido.");
            }
        }

        var usesCredit = command.Payments.Any(p => (PaymentMethod)p.Method == PaymentMethod.Credit);
        if (usesCredit && command.CustomerId is null)
        {
            return Result<SaleResponse>.Failure(
                "sale.customer_required",
                "Debe indicar un cliente para ventas en cuenta corriente.");
        }

        var policy = await _policyFactory.ForCurrentTenantAsync(cancellationToken);
        foreach (var line in command.Lines)
        {
            var lineCheck = policy.ValidateSaleLine(
                new StockLineContext(line.ProductId, line.Quantity, line.StockLotId));
            if (!lineCheck.IsSuccess)
            {
                return Result<SaleResponse>.Failure(lineCheck.ErrorCode!, lineCheck.Error!);
            }
        }

        var cashSessionId = await _cashSession.GetOpenSessionIdAsync(cancellationToken);
        if (cashSessionId is null)
        {
            return Result<SaleResponse>.Failure(
                "cash.session_required",
                "Debe abrir la caja para registrar ventas. Vaya a Caja e inicie un turno.");
        }

        var entitlementCheck = await _entitlements.EnsureCanRecordSaleAsync(cancellationToken);
        if (!entitlementCheck.IsSuccess)
        {
            return Result<SaleResponse>.Failure(
                entitlementCheck.ErrorCode!,
                entitlementCheck.Error!);
        }

        // Misma combinación producto+lote se agrupa; lotes distintos no se mezclan.
        var merged = command.Lines
            .GroupBy(l => new { l.ProductId, l.StockLotId })
            .Select(g => (
                ProductId: g.Key.ProductId,
                StockLotId: g.Key.StockLotId,
                Quantity: g.Sum(x => x.Quantity)))
            .ToList();

        var tenantId = _currentUser.TenantId?.Trim();
        if (string.IsNullOrEmpty(tenantId))
        {
            return Result<SaleResponse>.Failure(
                "sale.tenant_required",
                "No se pudo determinar el comercio (tenant). Vuelva a autenticarse.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var saleId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var totalNet = 0m;
        var totalTax = 0m;
        var lineResponses = new List<SaleLineResponse>();

        try
        {
            var currentUserId = _currentUser.UserId;
            string? createdByUserId = string.IsNullOrWhiteSpace(currentUserId) ? null : currentUserId;
            var createdByUserName = createdByUserId ?? "—";
            if (!string.IsNullOrEmpty(currentUserId) && !string.IsNullOrEmpty(tenantId))
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

            Guid? customerId = null;
            if (command.CustomerId is { } requestedCustomerId)
            {
                var customerExists = await _db.Customers
                    .AsNoTracking()
                    .AnyAsync(c => c.Id == requestedCustomerId && c.TenantId == tenantId, cancellationToken);
                if (!customerExists)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<SaleResponse>.Failure(
                        "sale.invalid_customer",
                        "El cliente no existe o no pertenece a este comercio.");
                }

                customerId = requestedCustomerId;
            }
            else if (usesCredit)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<SaleResponse>.Failure(
                    "sale.customer_required",
                    "Debe indicar un cliente para ventas en cuenta corriente.");
            }

            var sale = new Sale
            {
                Id = saleId,
                Date = now,
                TotalNet = 0m,
                TotalTax = 0m,
                TotalAmount = 0m,
                CreatedAt = now,
                CreatedByUserId = createdByUserId,
                CreatedByUserName = createdByUserName,
                CashSessionId = cashSessionId,
                CustomerId = customerId
            };

            foreach (var (productId, stockLotId, quantity) in merged)
            {
                var product = await _db.Products
                    .FirstOrDefaultAsync(
                        p => p.Id == productId && p.TenantId == tenantId,
                        cancellationToken);

                if (product is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<SaleResponse>.Failure(
                        "sale.invalid_product",
                        "Uno o más productos no existen o no pertenecen a este comercio.");
                }

                var apply = new StockApplyContext
                {
                    Product = product,
                    Quantity = quantity,
                    StockLotId = stockLotId,
                    ReferenceId = saleId,
                    CreatedByUserId = createdByUserId ?? currentUserId ?? string.Empty
                };

                var stockResult = await policy.ApplySaleAsync(apply, cancellationToken);
                if (!stockResult.IsSuccess)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<SaleResponse>.Failure(stockResult.ErrorCode!, stockResult.Error!);
                }

                var extJson = string.IsNullOrWhiteSpace(product.ExtendedDataJson) ? "{}" : product.ExtendedDataJson;

                // Farmacia FEFO multi-lote: una SaleDetail/línea por asignación.
                // Otros rubros (o un solo lote): una sola línea con la cantidad pedida.
                var detailSlices = apply.AppliedAllocations.Count > 0
                    ? apply.AppliedAllocations
                        .Select(a => (Qty: a.Quantity, LotId: (Guid?)a.StockLotId, LotNumber: (string?)a.LotNumber))
                        .ToList()
                    : [(
                        Qty: quantity,
                        LotId: apply.AppliedStockLotId ?? stockLotId,
                        LotNumber: apply.AppliedLotNumber
                    )];

                foreach (var (sliceQty, sliceLotId, sliceLotNumber) in detailSlices)
                {
                    var lineNet = decimal.Round(
                        product.NetPrice * sliceQty,
                        2,
                        MidpointRounding.AwayFromZero);
                    var lineTax = decimal.Round(
                        lineNet * (product.TaxRate / 100m),
                        2,
                        MidpointRounding.AwayFromZero);
                    totalNet += lineNet;
                    totalTax += lineTax;

                    var detailId = Guid.NewGuid();
                    sale.Details.Add(
                        new SaleDetail
                        {
                            Id = detailId,
                            SaleId = saleId,
                            ProductId = product.Id,
                            ProductName = product.Name,
                            ProductExtendedDataJson = extJson,
                            Quantity = sliceQty,
                            StockLotId = sliceLotId,
                            LineNetSubtotal = lineNet,
                            LineTaxAmount = lineTax,
                            UnitNetPrice = product.NetPrice,
                            TaxRate = product.TaxRate
                        });

                    lineResponses.Add(
                        new SaleLineResponse
                        {
                            Id = detailId,
                            ProductId = product.Id,
                            ProductName = product.Name,
                            ProductExtendedDataJson = extJson,
                            Quantity = sliceQty,
                            LineNetSubtotal = lineNet,
                            LineTaxAmount = lineTax,
                            UnitNetPrice = product.NetPrice,
                            TaxRate = product.TaxRate,
                            StockLotId = sliceLotId,
                            LotNumber = sliceLotNumber
                        });
                }
            }

            sale.TotalNet = totalNet;
            sale.TotalTax = totalTax;
            sale.TotalAmount = totalNet + totalTax;

            var paymentSum = decimal.Round(
                command.Payments.Sum(p => p.Amount),
                2,
                MidpointRounding.AwayFromZero);
            if (paymentSum != sale.TotalAmount)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<SaleResponse>.Failure(
                    "sale.payment_mismatch",
                    $"La suma de pagos ({paymentSum:0.00}) debe coincidir con el total ({sale.TotalAmount:0.00}).");
            }

            var paymentResponses = new List<SalePaymentResponse>();
            var creditCharges = new List<decimal>();
            foreach (var payment in command.Payments)
            {
                var paymentId = Guid.NewGuid();
                var method = (PaymentMethod)payment.Method;
                var amount = decimal.Round(payment.Amount, 2, MidpointRounding.AwayFromZero);
                sale.Payments.Add(
                    new SalePayment
                    {
                        Id = paymentId,
                        SaleId = saleId,
                        Method = method,
                        Amount = amount,
                        CreatedAt = now
                    });
                paymentResponses.Add(
                    new SalePaymentResponse
                    {
                        Id = paymentId,
                        Method = (int)method,
                        Amount = amount
                    });

                if (method == PaymentMethod.Credit)
                    creditCharges.Add(amount);
            }

            _db.Sales.Add(sale);

            if (creditCharges.Count > 0)
            {
                // customerId is guaranteed when usesCredit (validated above).
                var resolvedCustomerId = customerId!.Value;
                var previousBalance = await _db.CustomerAccountMovements
                    .Where(m => m.CustomerId == resolvedCustomerId)
                    .OrderByDescending(m => m.CreatedAt)
                    .ThenByDescending(m => m.Id)
                    .Select(m => (decimal?)m.BalanceAfter)
                    .FirstOrDefaultAsync(cancellationToken) ?? 0m;

                var runningBalance = previousBalance;
                foreach (var amount in creditCharges)
                {
                    runningBalance = decimal.Round(
                        runningBalance + amount,
                        2,
                        MidpointRounding.AwayFromZero);
                    _db.CustomerAccountMovements.Add(
                        new CustomerAccountMovement
                        {
                            Id = Guid.NewGuid(),
                            CustomerId = resolvedCustomerId,
                            Type = CustomerAccountMovementType.Charge,
                            Amount = amount,
                            BalanceAfter = runningBalance,
                            SaleId = saleId,
                            Notes = null,
                            SettlementMethod = null,
                            CashSessionId = null,
                            CreatedByUserId = createdByUserId,
                            CreatedAt = now
                        });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<SaleResponse>.Ok(
                new SaleResponse
                {
                    Id = saleId,
                    Date = now,
                    TotalNet = totalNet,
                    TotalTax = totalTax,
                    TotalAmount = sale.TotalAmount,
                    CustomerId = customerId,
                    Lines = lineResponses,
                    Payments = paymentResponses
                });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
