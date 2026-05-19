namespace POS.Domain.Common;

/// <summary>
/// Contrato de entidades con aislamiento multi-tenant (p. ej. CUIT/slug de tenant para facturación y datos).
/// </summary>
public interface ITenantEntity
{
    string TenantId { get; set; }
}
