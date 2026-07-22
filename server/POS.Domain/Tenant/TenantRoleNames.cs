namespace POS.Domain.Tenant;

/// <summary>Roles de Identity bajo el prefijo <c>Tenant.*</c> (usuarios de un negocio).</summary>
public static class TenantRoleNames
{
    public const string Admin = "Tenant.Admin";

    public const string Cashier = "Tenant.Cashier";

    public const string Stock = "Tenant.Stock";

    public static IReadOnlyList<string> All { get; } =
    [
        Admin,
        Cashier,
        Stock
    ];

    public static bool IsKnownRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;
        var t = role.Trim();
        return All.Any(x => x.Equals(t, StringComparison.Ordinal));
    }

    public static string Normalize(string role)
    {
        var t = role.Trim();
        foreach (var known in All)
        {
            if (known.Equals(t, StringComparison.OrdinalIgnoreCase))
                return known;
        }

        throw new ArgumentException($"Rol de tenant desconocido: {role}", nameof(role));
    }
}
