using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class Provider : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>CUIT u otro identificador fiscal (único por criterio de negocio: validación en aplicación si aplica).</summary>
    public string TaxId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
}
