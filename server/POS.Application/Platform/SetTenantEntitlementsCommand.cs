namespace POS.Application.Platform;

public sealed record SetTenantEntitlementsCommand(
    string TenantId,
    int? MaxProducts,
    int? MaxTenantUsers,
    bool SalesEnabled,
    string Justification);
