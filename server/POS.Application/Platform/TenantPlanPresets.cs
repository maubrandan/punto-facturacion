using POS.Application.Contracts.Platform;

namespace POS.Application.Platform;

/// <summary>
/// Presets comerciales de onboarding (no se persisten como entidad).
/// Rubro (<c>BusinessType</c>) es independiente del plan.
/// </summary>
public static class TenantPlanPresets
{
    public const string Starter = "Starter";
    public const string Pro = "Pro";
    public const string Unlimited = "Unlimited";

    public static readonly IReadOnlyList<string> All = [Starter, Pro, Unlimited];

    public static bool IsKnown(string? planCode) =>
        !string.IsNullOrWhiteSpace(planCode)
        && All.Contains(planCode.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string planCode)
    {
        var trimmed = planCode.Trim();
        foreach (var known in All)
        {
            if (known.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                return known;
        }

        return trimmed;
    }

    public static TenantEntitlementsDto Resolve(string? planCode)
    {
        if (string.IsNullOrWhiteSpace(planCode))
            return UnlimitedCaps();

        return Normalize(planCode) switch
        {
            Starter => new TenantEntitlementsDto
            {
                MaxProducts = 100,
                MaxTenantUsers = 3,
                SalesEnabled = true
            },
            Pro => new TenantEntitlementsDto
            {
                MaxProducts = 2000,
                MaxTenantUsers = 20,
                SalesEnabled = true
            },
            _ => UnlimitedCaps()
        };
    }

    /// <summary>
    /// Plan (si hay) + overrides explícitos del comando.
    /// </summary>
    public static TenantEntitlementsDto ResolveForCreate(CreatePlatformTenantCommand command)
    {
        var fromPlan = Resolve(command.PlanCode);
        return new TenantEntitlementsDto
        {
            MaxProducts = command.MaxProducts ?? fromPlan.MaxProducts,
            MaxTenantUsers = command.MaxTenantUsers ?? fromPlan.MaxTenantUsers,
            SalesEnabled = command.SalesEnabled ?? fromPlan.SalesEnabled
        };
    }

    /// <summary>
    /// Si los caps coinciden exactamente con un preset conocido, devuelve su código; si no, null (custom).
    /// </summary>
    public static string? MatchPlanCode(TenantEntitlementsDto entitlements)
    {
        foreach (var code in All)
        {
            var preset = Resolve(code);
            if (preset.MaxProducts == entitlements.MaxProducts
                && preset.MaxTenantUsers == entitlements.MaxTenantUsers
                && preset.SalesEnabled == entitlements.SalesEnabled)
            {
                return code;
            }
        }

        return null;
    }

    public static TenantEntitlementsDto WithMatchedPlan(TenantEntitlementsDto entitlements) =>
        new()
        {
            MaxProducts = entitlements.MaxProducts,
            MaxTenantUsers = entitlements.MaxTenantUsers,
            SalesEnabled = entitlements.SalesEnabled,
            MatchedPlanCode = MatchPlanCode(entitlements)
        };

    private static TenantEntitlementsDto UnlimitedCaps() =>
        new()
        {
            MaxProducts = null,
            MaxTenantUsers = null,
            SalesEnabled = true
        };
}
