using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Customers;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Customers;

public sealed class CustomerAccountQueryService : ICustomerAccountQueryService
{
    private readonly ApplicationDbContext _db;

    public CustomerAccountQueryService(ApplicationDbContext db) => _db = db;

    public async Task<Result<CustomerAccountResponse>> GetAccountAsync(
        Guid customerId,
        int recentLimit = 20,
        CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return Result<CustomerAccountResponse>.Failure(
                "customer.not_found",
                "Cliente no encontrado.");
        }

        var take = Math.Clamp(recentLimit, 1, 100);
        var movements = await _db.CustomerAccountMovements
            .AsNoTracking()
            .Where(m => m.CustomerId == customerId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        var balance = movements.Count > 0
            ? movements[0].BalanceAfter
            : await GetBalanceAsync(customerId, cancellationToken);

        return Result<CustomerAccountResponse>.Ok(
            new CustomerAccountResponse
            {
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                Balance = balance,
                RecentMovements = movements
                    .Select(MapMovement)
                    .ToList()
            });
    }

    public async Task<Result<IReadOnlyList<CustomerAccountMovementResponse>>> ListMovementsAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var exists = await _db.Customers
            .AsNoTracking()
            .AnyAsync(c => c.Id == customerId, cancellationToken);
        if (!exists)
        {
            return Result<IReadOnlyList<CustomerAccountMovementResponse>>.Failure(
                "customer.not_found",
                "Cliente no encontrado.");
        }

        var movements = await _db.CustomerAccountMovements
            .AsNoTracking()
            .Where(m => m.CustomerId == customerId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(500)
            .Select(m => new CustomerAccountMovementResponse
            {
                Id = m.Id,
                Type = (int)m.Type,
                Amount = m.Amount,
                BalanceAfter = m.BalanceAfter,
                SaleId = m.SaleId,
                Notes = m.Notes,
                SettlementMethod = m.SettlementMethod.HasValue ? (int)m.SettlementMethod.Value : null,
                CashSessionId = m.CashSessionId,
                CreatedByUserId = m.CreatedByUserId,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<CustomerAccountMovementResponse>>.Ok(movements);
    }

    private async Task<decimal> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var last = await _db.CustomerAccountMovements
            .AsNoTracking()
            .Where(m => m.CustomerId == customerId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Select(m => (decimal?)m.BalanceAfter)
            .FirstOrDefaultAsync(cancellationToken);
        return last ?? 0m;
    }

    internal static CustomerAccountMovementResponse MapMovement(CustomerAccountMovement m) =>
        new()
        {
            Id = m.Id,
            Type = (int)m.Type,
            Amount = m.Amount,
            BalanceAfter = m.BalanceAfter,
            SaleId = m.SaleId,
            Notes = m.Notes,
            SettlementMethod = m.SettlementMethod.HasValue ? (int)m.SettlementMethod.Value : null,
            CashSessionId = m.CashSessionId,
            CreatedByUserId = m.CreatedByUserId,
            CreatedAt = m.CreatedAt
        };
}
