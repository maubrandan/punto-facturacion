namespace POS.Application.Contracts.Platform;

public sealed class SetTenantFiscalProfileApiRequest
{
    public string TaxId { get; init; } = string.Empty;

    public int PointOfSale { get; init; } = 1;

    public bool IsProduction { get; init; }

    public bool IsEnabled { get; init; } = true;

    public string CertificateRef { get; init; } = string.Empty;

    public string PrivateKeyRef { get; init; } = string.Empty;

    public string Justification { get; init; } = string.Empty;
}
