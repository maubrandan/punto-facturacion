using POS.Domain.Entities;

namespace POS.Domain.Platform;

/// <summary>Transiciones válidas para operadores de plataforma.</summary>
public static class TenantLifecycleRules
{
    public static bool CanUpdate(TenantStatus status) => status != TenantStatus.Closed;

    public static bool CanSuspend(TenantStatus status) => status == TenantStatus.Active;

    public static bool CanClose(TenantStatus status) =>
        status == TenantStatus.Active || status == TenantStatus.Suspended;
}
