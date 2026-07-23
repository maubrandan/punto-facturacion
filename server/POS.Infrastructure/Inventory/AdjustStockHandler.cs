using FluentValidation;
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
    private readonly IValidator<AdjustStockCommand> _validator;

    public AdjustStockHandler(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IStockPolicyFactory policyFactory,
        IValidator<AdjustStockCommand> validator)
    {
        _db = db;
        _currentUser = currentUser;
        _policyFactory = policyFactory;
        _validator = validator;
    }

    public async Task<Result<StockAdjustmentResponse>> HandleAsync(
        AdjustStockCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var failure = validationResult.Errors[0];
            return Result<StockAdjustmentResponse>.Failure(
                string.IsNullOrWhiteSpace(failure.ErrorCode) ? "stock.adjustment" : failure.ErrorCode,
                failure.ErrorMessage);
        }

        var tenantId = _currentUser.TenantId?.Trim();
        if (string.IsNullOrEmpty(tenantId))
        {
            return Result<StockAdjustmentResponse>.Failure(
                "stock.tenant_required",
                "No se pudo determinar el comercio (tenant).");
        }

        var reasonCode = StockAdjustmentReasonCodes.Normalize(command.ReasonCode);

        var policy = await _policyFactory.ForCurrentTenantAsync(cancellationToken);
        var validation = policy.ValidateAdjustment(
            new StockAdjustContext(
                command.ProductId,
                command.QuantityDelta,
                reasonCode,
                command.Note,
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
                ReasonCode = reasonCode,
                ReasonNote = command.Note,
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
                    QuantityDelta = command.QuantityDelta,
                    ReasonCode = reasonCode
                });
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
