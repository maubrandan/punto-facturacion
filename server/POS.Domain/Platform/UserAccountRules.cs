namespace POS.Domain.Platform;

public static class UserAccountRules
{
    /// <summary>Valida la combinación (cuenta de negocio con tenant o cuenta plataforma con id sentinela).</summary>
    public static bool IsValidTenantPair(UserAccountKind kind, string? tenantId)
    {
        return kind switch
        {
            UserAccountKind.TenantUser => !string.IsNullOrWhiteSpace(tenantId) &&
                !string.Equals(
                    tenantId,
                    PlatformScope.ReservedTenantId,
                    StringComparison.Ordinal),
            UserAccountKind.PlatformUser => string.Equals(
                tenantId,
                PlatformScope.ReservedTenantId,
                StringComparison.Ordinal),
            _ => false
        };
    }
}
