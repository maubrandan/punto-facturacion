using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Application.Platform;

namespace POS.Application.Interfaces.Platform;

public interface IPlatformTenantLifecycleService
{
    Task<Result<TenantDetailDto>> CreateAsync(
        CreatePlatformTenantCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<TenantDetailDto>> UpdateAsync(
        UpdatePlatformTenantCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<TenantDetailDto>> SuspendAsync(
        SuspendPlatformTenantCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<TenantDetailDto>> CloseAsync(
        ClosePlatformTenantCommand command,
        CancellationToken cancellationToken = default);
}
