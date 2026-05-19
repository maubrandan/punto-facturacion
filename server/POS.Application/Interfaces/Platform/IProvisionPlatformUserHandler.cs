using POS.Application.Common;
using POS.Application.Platform;

namespace POS.Application.Interfaces.Platform;

public interface IProvisionPlatformUserHandler
{
    Task<Result<ProvisionPlatformUserResult>> HandleAsync(
        ProvisionPlatformUserCommand command,
        CancellationToken cancellationToken = default);
}
