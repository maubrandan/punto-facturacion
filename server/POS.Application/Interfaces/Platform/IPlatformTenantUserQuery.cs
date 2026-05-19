using POS.Application.Common;
using POS.Application.Contracts.Platform;

namespace POS.Application.Interfaces.Platform;

public interface IPlatformTenantUserQuery
{
    Task<Result<TenantUserDirectoryPageDto>> ListUsersAsync(
        string tenantId,
        int page,
        int pageSize,
        string? emailContains,
        CancellationToken cancellationToken = default);
}
