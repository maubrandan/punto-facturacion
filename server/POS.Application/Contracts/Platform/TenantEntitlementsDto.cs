namespace POS.Application.Contracts.Platform;

public sealed class TenantEntitlementsDto
{
    public int? MaxProducts { get; init; }

    public int? MaxTenantUsers { get; init; }

    public bool SalesEnabled { get; init; } = true;
}
