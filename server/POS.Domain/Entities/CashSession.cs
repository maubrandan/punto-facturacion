using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class CashSession : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public DateTime OpeningDate { get; set; } = DateTime.UtcNow;

    public DateTime? ClosingDate { get; set; }

    public decimal InitialAmount { get; set; }

    /// <summary>Calculado al cierre: apertura + ventas - compras - gastos (todo de la sesión).</summary>
    public decimal? ExpectedAmount { get; set; }

    /// <summary>Monto en efectivo contado físicamente al cierre.</summary>
    public decimal? CountedAmount { get; set; }

    /// <summary>Contado - esperado (sobrante positivo, faltante negativo).</summary>
    public decimal? Difference { get; set; }

    public CashSessionState State { get; set; } = CashSessionState.Open;

    public string? UserId { get; set; }

    public ICollection<Sale> Sales { get; set; } = new List<Sale>();

    public ICollection<SaleReturn> SaleReturns { get; set; } = new List<SaleReturn>();

    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
