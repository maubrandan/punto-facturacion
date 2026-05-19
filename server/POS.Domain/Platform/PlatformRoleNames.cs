namespace POS.Domain.Platform;

/// <summary>Roles de Identity bajo el prefijo <c>Platform.*</c> (ADR 0002).</summary>
public static class PlatformRoleNames
{
    public const string SuperAdmin = "Platform.SuperAdmin";

    public const string Operations = "Platform.Operations";

    public const string Support = "Platform.Support";

    public const string SupportReadOnly = "Platform.SupportReadOnly";

    /// <summary>Conjunto completo: cualquier asignación debe ser uno de estos nombres.</summary>
    public static IReadOnlyList<string> All { get; } = new[]
    {
        SuperAdmin,
        Operations,
        Support,
        SupportReadOnly
    };

    public static bool IsKnownRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;
        var t = role.Trim();
        return All.Any(x => x.Equals(t, StringComparison.Ordinal));
    }
}
