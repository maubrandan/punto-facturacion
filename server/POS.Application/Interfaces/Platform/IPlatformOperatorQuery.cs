using POS.Application.Contracts.Platform;

namespace POS.Application.Interfaces.Platform;

public interface IPlatformOperatorQuery
{
    Task<PlatformOperatorDirectoryPageDto> ListAsync(
        int page,
        int pageSize,
        string? emailContains,
        string? role,
        CancellationToken cancellationToken = default);
}
