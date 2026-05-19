using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Purchases;
using POS.Application.Interfaces;
using POS.Application.Purchases;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Purchases;

public sealed class CreatePurchaseHandler : ICreatePurchaseHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ICashSessionService _cashSession;

    public CreatePurchaseHandler(ApplicationDbContext db, ICashSessionService cashSession)
    {
        _db = db;
        _cashSession = cashSession;
    }

    public async Task<Result<PurchaseResponse>> HandleAsync(
        CreatePurchaseCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Lines.Count == 0)
        {
            return Result<PurchaseResponse>.Failure("purchase.validation", "Debe haber al menos una línea de compra.");
        }

        if (command.Lines.Any(l => l.Quantity <= 0))
        {
            return Result<PurchaseResponse>.Failure("purchase.validation", "La cantidad en cada línea debe ser mayor a cero.");
        }

        if (command.Lines.Any(l => l.UnitCost < 0m))
        {
            return Result<PurchaseResponse>.Failure("purchase.validation", "El costo unitario no puede ser negativo.");
        }

        var provider = await _db.Set<Provider>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == command.ProviderId, cancellationToken);

        if (provider is null)
        {
            return Result<PurchaseResponse>.Failure(
                "purchase.invalid_provider",
                "El proveedor no existe o no pertenece a este comercio.");
        }

        var cashSessionId = await _cashSession.GetOpenSessionIdAsync(cancellationToken);
        if (cashSessionId is null)
        {
            return Result<PurchaseResponse>.Failure(
                "cash.session_required",
                "Debe abrir la caja para registrar compras de turno. Vaya a Caja e inicie un turno.");
        }

        var merged = command.Lines
            .GroupBy(l => l.ProductId)
            .Select(g =>
            {
                var qty = g.Sum(x => x.Quantity);
                var costSum = g.Sum(x => x.Quantity * x.UnitCost);
                var unit = qty > 0
                    ? decimal.Round(costSum / qty, 4, MidpointRounding.AwayFromZero)
                    : 0m;
                return (ProductId: g.Key, Quantity: qty, UnitCost: unit);
            })
            .ToList();

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var purchaseId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var lineResponses = new List<PurchaseLineResponse>();
        var purchaseDate = ToUtc(command.Date);

        var purchase = new Purchase
        {
            Id = purchaseId,
            ProviderId = provider.Id,
            Date = purchaseDate,
            Total = 0m,
            InvoiceNumber = command.InvoiceNumber?.Trim() ?? string.Empty,
            CreatedAt = now,
            CashSessionId = cashSessionId
        };

        _db.Set<Purchase>().Add(purchase);

        try
        {
            foreach (var (productId, quantity, unitCost) in merged)
            {
                var product = await _db.Products
                    .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

                if (product is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<PurchaseResponse>.Failure(
                        "purchase.invalid_product",
                        "Uno o más productos no existen o no pertenecen a este comercio.");
                }

                product.Stock += quantity;
                product.LastCost = unitCost;

                var subtotal = decimal.Round(quantity * unitCost, 2, MidpointRounding.AwayFromZero);
                var detailId = Guid.NewGuid();
                var sku = string.IsNullOrWhiteSpace(product.SKU) ? string.Empty : product.SKU;

                _db.Set<PurchaseDetail>().Add(
                    new PurchaseDetail
                    {
                        Id = detailId,
                        PurchaseId = purchaseId,
                        ProductId = product.Id,
                        Quantity = quantity,
                        UnitCost = unitCost,
                        Subtotal = subtotal,
                        ProductName = product.Name,
                        ProductSku = sku
                    });

                lineResponses.Add(
                    new PurchaseLineResponse
                    {
                        Id = detailId,
                        ProductId = product.Id,
                        ProductName = product.Name,
                        ProductSku = sku,
                        Quantity = quantity,
                        UnitCost = unitCost,
                        Subtotal = subtotal
                    });
            }

            var total = decimal.Round(
                lineResponses.Sum(l => l.Subtotal),
                2,
                MidpointRounding.AwayFromZero);
            purchase.Total = total;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PurchaseResponse>.Ok(
            new PurchaseResponse
            {
                Id = purchaseId,
                ProviderId = provider.Id,
                ProviderName = provider.Name,
                Date = command.Date,
                InvoiceNumber = command.InvoiceNumber?.Trim() ?? string.Empty,
                Total = purchase.Total,
                Lines = lineResponses
            });
    }

    private static DateTime ToUtc(DateTime d)
    {
        return d.Kind switch
        {
            DateTimeKind.Utc => d,
            DateTimeKind.Local => d.ToUniversalTime(),
            _ => DateTime.SpecifyKind(d, DateTimeKind.Utc)
        };
    }
}
