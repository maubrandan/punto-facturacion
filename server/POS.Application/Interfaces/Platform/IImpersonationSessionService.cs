using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Application.Platform;

namespace POS.Application.Interfaces.Platform;

public interface IImpersonationSessionService
{
    Task<Result<ImpersonationSessionResponseDto>> StartAsync(
        StartImpersonationSessionCommand command,
        CancellationToken cancellationToken = default);
}
