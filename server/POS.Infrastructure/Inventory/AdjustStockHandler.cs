using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Inventory;
using POS.Application.Interfaces;
using POS.Application.Inventory;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Inventory;

public sealed class AdjustStockHandler : IAdjustStockHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IStockPolicyFactory _policyFactory;

    public AdjustStockHandler(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IStockPolicyFactory policyFactory)
    {
        _db = db;
        _currentUser = currentUser;
        _policyFactory = policyFactory;
    }

    public async Task<Result<StockAdjustmentResponse>> HandleAsync(
        AdjustStockCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId?.Trim();
        if (string.IsNullOrEmpty(tenantId))
        {
            return Result<StockAdjustmentResponse>.Failure(
                "stock.tenant_required",
                "No se pudo determinar el comercio (tenant).");
        }

        var policy = await _policyFactory.ForCurrentTenantAsync(cancellationToken);
        var validation = policy.ValidateAdjustment(
            new StockAdjustContext(
                command.ProductId,
                command.QuantityDelta,
                command.Reason,
                command.StockLotId,
                command.LotNumber,
                command.ExpirationDate));

        if (!validation.IsSuccess)
        {
            return Result<StockAdjustmentResponse>.Failure(validation.ErrorCode!, validation.Error!);
        }

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == command.ProductId && p.TenantId == tenantId, cancellationToken);

        if (product is null)
        {
            return Result<StockAdjustmentResponse>.Failure("product.not_found", "Producto no encontrado.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var apply = new StockApplyContext
            {
                Product = product,
                Quantity = command.QuantityDelta,
                StockLotId = command.StockLotId,
                LotNumber = command.LotNumber,
                ExpirationDate = command.ExpirationDate,
                Reason = command.Reason,
                CreatedByUserId = _currentUser.UserId ?? string.Empty
            };

            var result = await policy.ApplyAdjustmentAsync(apply, cancellationToken);
            if (!result.IsSuccess)
            {
                await tx.RollbackAsync(cancellationToken);
                return Result<StockAdjustmentResponse>.Failure(result.ErrorCode!, result.Error!);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return Result<StockAdjustmentResponse>.Ok(
                new StockAdjustmentResponse
                {
                    ProductId = product.Id,
                    StockAfter = product.Stock,
                    StockLotId = apply.AppliedStockLotId,
                    LotNumber = apply.AppliedLotNumber,
                    QuantityDelta = command.QuantityDelta
                });
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
