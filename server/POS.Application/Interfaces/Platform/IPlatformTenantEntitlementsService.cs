using POS.Application.Common;
using POS.Application.Contracts.Platform;

namespace POS.Application.Interfaces.Platform;

public interface IPlatformTenantEntitlementsService
{
    Task<Result<TenantEntitlementsDto>> GetAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<Result<TenantEntitlementsDto>> SetAsync(
        string tenantId,
        TenantEntitlementsDto values,
        string justification,
        CancellationToken cancellationToken = default);
}
