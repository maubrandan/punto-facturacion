using POS.Application.Common;
using POS.Application.Contracts.Fiscal;

namespace POS.Application.Interfaces.Platform;

public interface IPlatformTenantFiscalProfileService
{
    Task<Result<TenantFiscalProfileResponse>> GetAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<Result<TenantFiscalProfileResponse>> UpsertAsync(
        string tenantId,
        UpsertTenantFiscalProfileRequest values,
        string justification,
        CancellationToken cancellationToken = default);
}
