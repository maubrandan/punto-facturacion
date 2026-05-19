namespace POS.Domain.Platform;

/// <summary>
/// Valores fijos acordados en ADR 0001/0002: los usuarios de plataforma no usan un tenant de negocio;
/// el <c>TenantId</c> almacenado se fija a este id sentinela para respetar columnas NOT NULL y
/// <see cref="ITenantEntity"/>; no se interpreta como negocio en el JWT/claims (Fase 4+).
/// </summary>
public static class PlatformScope
{
    public const string ReservedTenantId = "00000000-0000-0000-0000-000000000000";

    /// <summary>Valor almacenado en <see cref="Entities.ApplicationUser.BusinessType"/> para cuentas plataforma.</summary>
    public const string PlaceholderBusinessType = "Platform";
}
