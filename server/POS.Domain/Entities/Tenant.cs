namespace POS.Domain.Entities;

/// <summary>
/// Negocio/tenant. No implementa <see cref="Common.ITenantEntity"/>: no está “dentro de” otro tenant.
/// </summary>
public class Tenant
{
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Nombre comercial / razón social visible en la UI.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Contacto operativo (facturación / avisos); opcional.</summary>
    public string? ContactEmail { get; set; }

    public TenantStatus Status { get; set; } = TenantStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? SuspendedAt { get; set; }

    public DateTime? ClosedAt { get; set; }
}
