using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Application.Platform;

namespace POS.Application.Interfaces.Platform;

public interface IPlatformOperatorAdminService
{
    Task<Result<PlatformOperatorSummaryDto>> ProvisionAsync(
        ProvisionPlatformUserCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<PlatformOperatorSummaryDto>> UpdateAsync(
        UpdatePlatformOperatorCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<PlatformOperatorSummaryDto>> BlockAsync(
        BlockPlatformOperatorCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<PlatformOperatorSummaryDto>> UnblockAsync(
        UnblockPlatformOperatorCommand command,
        CancellationToken cancellationToken = default);
}
