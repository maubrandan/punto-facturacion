using POS.Domain.Common;

namespace POS.Domain.Entities;

public sealed class TenantFiscalProfile : ITenantEntity
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string TaxId { get; set; } = string.Empty;

    public int PointOfSale { get; set; }

    public bool IsProduction { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string CertificateRef { get; set; } = string.Empty;

    public string PrivateKeyRef { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
