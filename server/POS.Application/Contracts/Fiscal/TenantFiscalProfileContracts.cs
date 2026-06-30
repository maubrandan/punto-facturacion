using POS.Domain.Entities;

namespace POS.Application.Contracts.Fiscal;

public sealed class TenantFiscalProfileResponse
{
    public Guid Id { get; init; }

    public string TaxId { get; init; } = string.Empty;

    public int PointOfSale { get; init; }

    public bool IsProduction { get; init; }

    public bool IsEnabled { get; init; }

    public string CertificateRef { get; init; } = string.Empty;

    public string PrivateKeyRef { get; init; } = string.Empty;

    public DateTime UpdatedAtUtc { get; init; }

    public static TenantFiscalProfileResponse FromEntity(TenantFiscalProfile entity) =>
        new()
        {
            Id = entity.Id,
            TaxId = entity.TaxId,
            PointOfSale = entity.PointOfSale,
            IsProduction = entity.IsProduction,
            IsEnabled = entity.IsEnabled,
            CertificateRef = entity.CertificateRef,
            PrivateKeyRef = entity.PrivateKeyRef,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
}

public sealed class UpsertTenantFiscalProfileRequest
{
    public string TaxId { get; init; } = string.Empty;

    public int PointOfSale { get; init; } = 1;

    public bool IsProduction { get; init; }

    public bool IsEnabled { get; init; } = true;

    public string CertificateRef { get; init; } = string.Empty;

    public string PrivateKeyRef { get; init; } = string.Empty;
}
