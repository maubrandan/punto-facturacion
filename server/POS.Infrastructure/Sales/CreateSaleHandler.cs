using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Sales;
using POS.Application.Interfaces;
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

    public CreateSaleHandler(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        ICashSessionService cashSession,
        ITenantEntitlementGuard entitlements)
    {
        _db = db;
        _currentUser = currentUser;
        _cashSession = cashSession;
        _entitlements = entitlements;
    }

    public async Task<Result<SaleResponse>> HandleAsync(
        CreateSaleCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Lines.Count == 0)
        {
            return Result<SaleResponse>.Failure("sale.validation", "Debe haber al menos una línea de venta.");
        }

        if (command.Lines.Any(l => l.Quantity <= 0))
        {
            return Result<SaleResponse>.Failure("sale.validation", "La cantidad en cada línea debe ser mayor a cero.");
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

        var merged = command.Lines
            .GroupBy(l => l.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(x => x.Quantity)))
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
            string? createdByUserId = null;
            var createdByUserName = "—";
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
                        ? (appUser.Email ?? appUser.UserName ?? "—")
                        : appUser.FullName;
                }
            }

            foreach (var (productId, quantity) in merged)
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

                var lineNet = decimal.Round(product.NetPrice * quantity, 2, MidpointRounding.AwayFromZero);
                var lineTax = decimal.Round(lineNet * (product.TaxRate / 100m), 2, MidpointRounding.AwayFromZero);
                totalNet += lineNet;
                totalTax += lineTax;
                product.Stock -= quantity;
                var extJson = string.IsNullOrWhiteSpace(product.ExtendedDataJson) ? "{}" : product.ExtendedDataJson;

                var detailId = Guid.NewGuid();
                lineResponses.Add(
                    new SaleLineResponse
                    {
                        Id = detailId,
                        ProductId = product.Id,
                        ProductName = product.Name,
                        ProductExtendedDataJson = extJson,
                        Quantity = quantity,
                        LineNetSubtotal = lineNet,
                        LineTaxAmount = lineTax,
                        UnitNetPrice = product.NetPrice,
                        TaxRate = product.TaxRate
                    });
            }

            var totalAmount = totalNet + totalTax;

            var sale = new Sale
            {
                Id = saleId,
                Date = now,
                TotalNet = totalNet,
                TotalTax = totalTax,
                TotalAmount = totalAmount,
                CreatedAt = now,
                CreatedByUserId = createdByUserId,
                CreatedByUserName = createdByUserName,
                CashSessionId = cashSessionId
            };

            for (var i = 0; i < lineResponses.Count; i++)
            {
                var line = lineResponses[i];
                sale.Details.Add(
                    new SaleDetail
                    {
                        Id = line.Id,
                        SaleId = saleId,
                        ProductId = line.ProductId,
                        ProductName = line.ProductName,
                        ProductExtendedDataJson = line.ProductExtendedDataJson,
                        Quantity = line.Quantity,
                        LineNetSubtotal = line.LineNetSubtotal,
                        LineTaxAmount = line.LineTaxAmount,
                        UnitNetPrice = line.UnitNetPrice,
                        TaxRate = line.TaxRate
                    });
            }

            _db.Sales.Add(sale);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<SaleResponse>.Ok(
            new SaleResponse
            {
                Id = saleId,
                Date = now,
                TotalNet = totalNet,
                TotalTax = totalTax,
                TotalAmount = totalNet + totalTax,
                Lines = lineResponses
            });
    }
}
