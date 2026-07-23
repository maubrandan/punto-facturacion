namespace POS.Domain.Platform;

/// <summary>Invariantes de gestión de operadores de plataforma (consola SuperAdmin).</summary>
public static class PlatformOperatorRules
{
    public static bool IsSelf(string? actorUserId, string targetUserId) =>
        !string.IsNullOrWhiteSpace(actorUserId)
        && string.Equals(actorUserId.Trim(), targetUserId.Trim(), StringComparison.Ordinal);

    /// <summary>
    /// True si la acción (demote o block) dejaría al sistema sin ningún SuperAdmin activo.
    /// </summary>
    public static bool WouldRemoveLastActiveSuperAdmin(
        bool targetIsActiveSuperAdmin,
        bool actionRemovesSuperAdminPrivilege,
        int activeSuperAdminCount)
    {
        if (!actionRemovesSuperAdminPrivilege || !targetIsActiveSuperAdmin)
            return false;

        return activeSuperAdminCount <= 1;
    }
}
