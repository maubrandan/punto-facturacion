using POS.Application.Contracts.Fiscal;

namespace POS.Application.Platform;

public sealed record UpsertPlatformTenantFiscalProfileCommand(
    string TenantId,
    UpsertTenantFiscalProfileRequest Values,
    string Justification);
