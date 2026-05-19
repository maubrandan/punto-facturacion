namespace POS.Domain.Entities;

/// <summary>
/// Límites y flags de producto aplicados por plataforma (Fase 9). Una fila por tenant; ausencia de fila → sin límites numéricos y ventas habilitadas.
/// </summary>
public sealed class TenantEntitlement
{
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Máximo de productos; null sin tope.</summary>
    public int? MaxProducts { get; set; }

    /// <summary>Máximo de usuarios de negocio del tenant (<c>TenantUser</c>); null sin tope.</summary>
    public int? MaxTenantUsers { get; set; }

    public bool SalesEnabled { get; set; } = true;

    public DateTime UpdatedAtUtc { get; set; }
}
