using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class Expense : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime Date { get; set; } = DateTime.UtcNow;

    public Guid CategoryId { get; set; }

    public ExpenseCategory? Category { get; set; }

    public Guid? CashSessionId { get; set; }

    public CashSession? CashSession { get; set; }
}
