namespace POS.Application.Contracts.Platform;

public sealed class SetTenantEntitlementsApiRequest
{
    public int? MaxProducts { get; init; }

    public int? MaxTenantUsers { get; init; }

    public required bool SalesEnabled { get; init; }

    public required string Justification { get; init; }
}
