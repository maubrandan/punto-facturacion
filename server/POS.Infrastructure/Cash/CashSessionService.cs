using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Cash;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Cash;

public sealed class CashSessionService : ICashSessionService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CashSessionService(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid?> GetOpenSessionIdAsync(CancellationToken cancellationToken = default)
    {
        var s = await _db.CashSessions
            .AsNoTracking()
            .Where(x => x.State == CashSessionState.Open)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return s == default ? null : s;
    }

    public async Task<Result<CashSessionOpenResponse>> OpenSessionAsync(
        decimal initialAmount,
        CancellationToken cancellationToken = default)
    {
        if (initialAmount < 0m)
        {
            return Result<CashSessionOpenResponse>.Failure(
                "cash.validation",
                "El monto inicial no puede ser negativo.");
        }

        if (await _db.CashSessions.AnyAsync(x => x.State == CashSessionState.Open, cancellationToken))
        {
            return Result<CashSessionOpenResponse>.Failure(
                "cash.already_open",
                "Ya hay una sesión de caja abierta. Ciérrela antes de abrir otra.");
        }

        var session = new CashSession
        {
            Id = Guid.NewGuid(),
            OpeningDate = DateTime.UtcNow,
            InitialAmount = initialAmount,
            State = CashSessionState.Open,
            UserId = _currentUser.UserId
        };

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await EnsureDefaultCategoriesAsync(cancellationToken);
            _db.CashSessions.Add(session);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<CashSessionOpenResponse>.Ok(
            new CashSessionOpenResponse
            {
                Id = session.Id,
                OpeningDate = session.OpeningDate,
                InitialAmount = session.InitialAmount,
                State = (int)session.State,
                UserId = session.UserId
            });
    }

    public async Task<Result<CashSessionCloseResponse>> CloseSessionAsync(
        decimal countedAmount,
        CancellationToken cancellationToken = default)
    {
        if (countedAmount < 0m)
        {
            return Result<CashSessionCloseResponse>.Failure(
                "cash.validation",
                "El monto contado no puede ser negativo.");
        }

        var session = await _db.CashSessions
            .FirstOrDefaultAsync(x => x.State == CashSessionState.Open, cancellationToken);

        if (session is null)
        {
            return Result<CashSessionCloseResponse>.Failure(
                "cash.not_open",
                "No hay una sesión de caja abierta para cerrar.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var (expected, _, _, _) = await ComputeExpected(
                session.Id,
                session.InitialAmount,
                cancellationToken);

            var expectedRounded = decimal.Round(expected, 2, MidpointRounding.AwayFromZero);
            var diff = decimal.Round(
                countedAmount - expectedRounded,
                2,
                MidpointRounding.AwayFromZero);
            var now = DateTime.UtcNow;

            session.ClosingDate = now;
            session.ExpectedAmount = expectedRounded;
            session.CountedAmount = countedAmount;
            session.Difference = diff;
            session.State = CashSessionState.Closed;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var closed = await _db.CashSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == session.Id, cancellationToken);
        if (closed is null)
        {
            return Result<CashSessionCloseResponse>.Failure("cash.unknown", "Error al leer el cierre.");
        }

        return Result<CashSessionCloseResponse>.Ok(
            new CashSessionCloseResponse
            {
                Id = closed.Id,
                InitialAmount = closed.InitialAmount,
                ExpectedAmount = closed.ExpectedAmount ?? 0m,
                CountedAmount = closed.CountedAmount ?? 0m,
                Difference = closed.Difference ?? 0m,
                ClosingDate = closed.ClosingDate
            });
    }

    public async Task<Result<ExpenseResponse>> RegisterExpenseAsync(
        RegisterExpenseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return Result<ExpenseResponse>.Failure("expense.validation", "La descripción es obligatoria.");
        }

        if (request.Amount <= 0m)
        {
            return Result<ExpenseResponse>.Failure("expense.validation", "El monto debe ser mayor a cero.");
        }

        var openId = await GetOpenSessionIdAsync(cancellationToken);
        if (openId is null)
        {
            return Result<ExpenseResponse>.Failure(
                "cash.session_required",
                "Debe abrir la caja para registrar gastos de turno.");
        }

        var category = await _db.ExpenseCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result<ExpenseResponse>.Failure("expense.invalid_category", "Categoría no encontrada.");
        }

        var when = request.Date is { } d
            ? d.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(d, DateTimeKind.Utc)
                : d.ToUniversalTime()
            : DateTime.UtcNow;

        var exp = new Expense
        {
            Id = Guid.NewGuid(),
            Description = request.Description.Trim(),
            Amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            Date = when,
            CategoryId = category.Id,
            CashSessionId = openId
        };

        _db.Expenses.Add(exp);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<ExpenseResponse>.Failure("expense.save", "No se pudo guardar el gasto.");
        }

        return Result<ExpenseResponse>.Ok(
            new ExpenseResponse
            {
                Id = exp.Id,
                Description = exp.Description,
                Amount = exp.Amount,
                Date = exp.Date,
                CategoryId = category.Id,
                CategoryName = category.Name,
                CashSessionId = openId
            });
    }

    public async Task<CashSessionSummaryResponse> GetCurrentSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var session = await _db.CashSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.State == CashSessionState.Open, cancellationToken);
        if (session is null)
        {
            return new CashSessionSummaryResponse
            {
                ProjectedAmount = 0m
            };
        }

        return await BuildSummaryForSessionAsync(session, cancellationToken);
    }

    private async Task<CashSessionSummaryResponse> BuildSummaryForSessionAsync(
        CashSession session,
        CancellationToken cancellationToken)
    {
        var id = session.Id;
        var totalSales = await _db.Sales
            .Where(s => s.CashSessionId == id)
            .SumAsync(s => s.TotalAmount, cancellationToken);

        var paymentRows = await (
                from p in _db.SalePayments.AsNoTracking()
                join s in _db.Sales.AsNoTracking() on p.SaleId equals s.Id
                where s.CashSessionId == id
                group p by p.Method
                into g
                select new { Method = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var totalCashFromSales = paymentRows
            .Where(r => r.Method == PaymentMethod.Cash)
            .Select(r => r.Total)
            .DefaultIfEmpty(0m)
            .Sum();
        var totalCard = paymentRows
            .Where(r => r.Method == PaymentMethod.Card)
            .Select(r => r.Total)
            .DefaultIfEmpty(0m)
            .Sum();
        var totalTransfer = paymentRows
            .Where(r => r.Method == PaymentMethod.Transfer)
            .Select(r => r.Total)
            .DefaultIfEmpty(0m)
            .Sum();
        var totalCredit = paymentRows
            .Where(r => r.Method == PaymentMethod.Credit)
            .Select(r => r.Total)
            .DefaultIfEmpty(0m)
            .Sum();

        var returnPaymentRows = await (
                from p in _db.SaleReturnPayments.AsNoTracking()
                join r in _db.SaleReturns.AsNoTracking() on p.SaleReturnId equals r.Id
                where r.CashSessionId == id
                group p by p.Method
                into g
                select new { Method = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var cashReturns = returnPaymentRows
            .Where(r => r.Method == PaymentMethod.Cash)
            .Select(r => r.Total)
            .DefaultIfEmpty(0m)
            .Sum();
        var cardReturns = returnPaymentRows
            .Where(r => r.Method == PaymentMethod.Card)
            .Select(r => r.Total)
            .DefaultIfEmpty(0m)
            .Sum();
        var transferReturns = returnPaymentRows
            .Where(r => r.Method == PaymentMethod.Transfer)
            .Select(r => r.Total)
            .DefaultIfEmpty(0m)
            .Sum();
        var creditReturns = returnPaymentRows
            .Where(r => r.Method == PaymentMethod.Credit)
            .Select(r => r.Total)
            .DefaultIfEmpty(0m)
            .Sum();

        // Cobros de CC en efectivo asociados a esta sesión (Amount es negativo → sumamos -Amount).
        var accountCashSettlements = await _db.CustomerAccountMovements
            .AsNoTracking()
            .Where(
                m => m.CashSessionId == id
                    && m.Type == CustomerAccountMovementType.Payment
                    && m.SettlementMethod == PaymentMethod.Cash)
            .SumAsync(m => -m.Amount, cancellationToken);

        var totalCash = totalCashFromSales - cashReturns + accountCashSettlements;
        totalCard -= cardReturns;
        totalTransfer -= transferReturns;
        totalCredit -= creditReturns;

        var totalPurchases = await _db.Purchases
            .Where(p => p.CashSessionId == id)
            .SumAsync(p => p.Total, cancellationToken);
        var totalExp = await _db.Expenses
            .Where(e => e.CashSessionId == id)
            .SumAsync(e => e.Amount, cancellationToken);

        var projected = session.InitialAmount + totalCash - totalPurchases - totalExp;
        return new CashSessionSummaryResponse
        {
            SessionId = session.Id,
            OpeningDate = session.OpeningDate,
            InitialAmount = session.InitialAmount,
            TotalSales = totalSales,
            TotalCashPayments = totalCash,
            TotalCardPayments = totalCard,
            TotalTransferPayments = totalTransfer,
            TotalCreditPayments = totalCredit,
            TotalPurchases = totalPurchases,
            TotalExpenses = totalExp,
            ProjectedAmount = decimal.Round(projected, 2, MidpointRounding.AwayFromZero)
        };
    }

    public async Task<IReadOnlyList<ExpenseCategoryResponse>> ListCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultCategoriesIfEmptyAsync(cancellationToken);
        return await _db.ExpenseCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(
                c => new ExpenseCategoryResponse
                {
                    Id = c.Id,
                    Name = c.Name
                })
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<ExpenseCategoryResponse>> CreateCategoryAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<ExpenseCategoryResponse>.Failure("category.validation", "El nombre es obligatorio.");
        }

        var entity = new ExpenseCategory
        {
            Id = Guid.NewGuid(),
            Name = name.Trim()
        };
        _db.ExpenseCategories.Add(entity);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<ExpenseCategoryResponse>.Failure(
                "category.duplicate",
                "No se pudo crear la categoría.");
        }

        return Result<ExpenseCategoryResponse>.Ok(
            new ExpenseCategoryResponse
            {
                Id = entity.Id,
                Name = entity.Name
            });
    }

    private async Task EnsureDefaultCategoriesAsync(CancellationToken cancellationToken)
    {
        if (await _db.ExpenseCategories.AnyAsync(cancellationToken))
            return;

        var names = new[] { "Flete", "Limpieza", "Otros" };
        foreach (var n in names)
        {
            _db.ExpenseCategories.Add(
                new ExpenseCategory
                {
                    Id = Guid.NewGuid(),
                    Name = n
                });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDefaultCategoriesIfEmptyAsync(CancellationToken cancellationToken)
    {
        if (await _db.ExpenseCategories.AnyAsync(cancellationToken))
            return;
        await EnsureDefaultCategoriesAsync(cancellationToken);
    }

    private async Task<(decimal Expected, decimal Sales, decimal Purch, decimal Exp)> ComputeExpected(
        Guid sessionId,
        decimal initial,
        CancellationToken cancellationToken)
    {
        var totalSales = await _db.Sales
            .Where(s => s.CashSessionId == sessionId)
            .SumAsync(s => s.TotalAmount, cancellationToken);
        var totalCashFromSales = await (
                from p in _db.SalePayments
                join s in _db.Sales on p.SaleId equals s.Id
                where s.CashSessionId == sessionId && p.Method == PaymentMethod.Cash
                select p.Amount)
            .SumAsync(cancellationToken);
        var cashReturns = await (
                from p in _db.SaleReturnPayments
                join r in _db.SaleReturns on p.SaleReturnId equals r.Id
                where r.CashSessionId == sessionId && p.Method == PaymentMethod.Cash
                select p.Amount)
            .SumAsync(cancellationToken);
        var accountCashSettlements = await _db.CustomerAccountMovements
            .Where(
                m => m.CashSessionId == sessionId
                    && m.Type == CustomerAccountMovementType.Payment
                    && m.SettlementMethod == PaymentMethod.Cash)
            .SumAsync(m => -m.Amount, cancellationToken);
        var totalCash = totalCashFromSales - cashReturns + accountCashSettlements;
        var totalPurch = await _db.Purchases
            .Where(p => p.CashSessionId == sessionId)
            .SumAsync(p => p.Total, cancellationToken);
        var totalExp = await _db.Expenses
            .Where(e => e.CashSessionId == sessionId)
            .SumAsync(e => e.Amount, cancellationToken);
        var exp = initial + totalCash - totalPurch - totalExp;
        return (exp, totalSales, totalPurch, totalExp);
    }
}
