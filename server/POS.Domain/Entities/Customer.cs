using POS.Domain.Common;

namespace POS.Domain.Entities;

/// <summary>Cliente del tenant (Factura A / directorio comercial).</summary>
public sealed class Customer : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>CUIT/CUIL u otro identificador fiscal (preferir 11 dígitos para Factura A).</summary>
    public string TaxId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
