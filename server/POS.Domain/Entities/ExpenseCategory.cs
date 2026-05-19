using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class ExpenseCategory : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
