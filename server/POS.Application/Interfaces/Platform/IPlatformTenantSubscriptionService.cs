using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Application.Platform;

namespace POS.Application.Interfaces.Platform;

public interface IPlatformTenantSubscriptionService
{
    Task<Result<TenantSubscriptionDto>> GetAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<Result<TenantSubscriptionDto>> UpdateAsync(
        UpdateTenantSubscriptionCommand command,
        CancellationToken cancellationToken = default);
}
