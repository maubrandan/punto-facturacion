namespace POS.Application.Platform;

/// <summary>Claims JWT para consola de plataforma (ADR 0002). El token POS multi-tenant no las incluye.</summary>
public static class PlatformClaimTypes
{
    public const string IsPlatform = "is_platform";

    /// <summary>Rol principal de plataforma (primer rol en el token).</summary>
    public const string PlatformRole = "platform_role";

    /// <summary>Valor <c>true</c> cuando el JWT es sesión de soporte en contexto tenant (Fase 7).</summary>
    public const string Impersonation = "impersonation";

    /// <summary>Motivo acotado para auditoría / UI (banner POS).</summary>
    public const string ImpersonationReason = "imp_reason";
}
