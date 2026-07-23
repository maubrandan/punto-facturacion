using POS.Domain.Entities;

namespace POS.Application.Billing;

/// <summary>Catálogo comercial de precios SaaS (ARS). No es factura fiscal POS.</summary>
public static class SaaSPlanPricing
{
    public const string DefaultCurrency = "ARS";

    public static decimal MonthlyAmount(string planCode) =>
        planCode.Trim().ToUpperInvariant() switch
        {
            "STARTER" => 15_000m,
            "PRO" => 45_000m,
            "UNLIMITED" => 99_000m,
            _ => 0m
        };

    /// <summary>Anual = 10 meses (2 de bonificación).</summary>
    public static decimal AmountFor(string planCode, BillingCycle cycle)
    {
        var monthly = MonthlyAmount(planCode);
        return cycle == BillingCycle.Yearly ? monthly * 10m : monthly;
    }
}
