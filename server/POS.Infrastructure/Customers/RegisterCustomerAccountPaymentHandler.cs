using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Customers;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Customers;

public sealed class RegisterCustomerAccountPaymentHandler : IRegisterCustomerAccountPaymentHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICashSessionService _cashSession;

    public RegisterCustomerAccountPaymentHandler(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        ICashSessionService cashSession)
    {
        _db = db;
        _currentUser = currentUser;
        _cashSession = cashSession;
    }

    public async Task<Result<RegisterCustomerAccountPaymentResponse>> HandleAsync(
        Guid customerId,
        RegisterCustomerAccountPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0m)
        {
            return Result<RegisterCustomerAccountPaymentResponse>.Failure(
                "customer_account.validation",
                "El monto debe ser mayor a cero.");
        }

        if (!Enum.IsDefined(typeof(PaymentMethod), request.Method)
            || (PaymentMethod)request.Method is PaymentMethod.Credit)
        {
            return Result<RegisterCustomerAccountPaymentResponse>.Failure(
                "customer_account.payment_invalid",
                "Medio de cobro no válido. Use efectivo, tarjeta o transferencia.");
        }

        var method = (PaymentMethod)request.Method;
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return Result<RegisterCustomerAccountPaymentResponse>.Failure(
                "customer.not_found",
                "Cliente no encontrado.");
        }

        Guid? cashSessionId = null;
        if (method == PaymentMethod.Cash)
        {
            cashSessionId = await _cashSession.GetOpenSessionIdAsync(cancellationToken);
            if (cashSessionId is null)
            {
                return Result<RegisterCustomerAccountPaymentResponse>.Failure(
                    "cash.session_required",
                    "Debe abrir la caja para registrar cobros de cuenta corriente en efectivo.");
            }
        }

        var amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        var signedAmount = -amount;
        var now = DateTime.UtcNow;
        var userId = string.IsNullOrWhiteSpace(_currentUser.UserId) ? null : _currentUser.UserId;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var previousBalance = await _db.CustomerAccountMovements
                .Where(m => m.CustomerId == customerId)
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Select(m => (decimal?)m.BalanceAfter)
                .FirstOrDefaultAsync(cancellationToken) ?? 0m;

            if (amount > previousBalance)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<RegisterCustomerAccountPaymentResponse>.Failure(
                    "customer_account.overpayment",
                    $"El monto ({amount:0.00}) supera el saldo pendiente ({previousBalance:0.00}).");
            }

            var balanceAfter = decimal.Round(
                previousBalance + signedAmount,
                2,
                MidpointRounding.AwayFromZero);

            var movement = new CustomerAccountMovement
            {
                Id = Guid.NewGuid(),
                CustomerId = customerId,
                Type = CustomerAccountMovementType.Payment,
                Amount = signedAmount,
                BalanceAfter = balanceAfter,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                SettlementMethod = method,
                CashSessionId = cashSessionId,
                CreatedByUserId = userId,
                CreatedAt = now
            };

            _db.CustomerAccountMovements.Add(movement);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<RegisterCustomerAccountPaymentResponse>.Ok(
                new RegisterCustomerAccountPaymentResponse
                {
                    MovementId = movement.Id,
                    Amount = signedAmount,
                    BalanceAfter = balanceAfter,
                    SettlementMethod = (int)method,
                    CashSessionId = cashSessionId,
                    CreatedAt = movement.CreatedAt
                });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
