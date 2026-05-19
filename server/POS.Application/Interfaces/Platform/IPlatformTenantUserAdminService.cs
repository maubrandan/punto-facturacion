using POS.Application.Common;
using POS.Application.Contracts.Platform;

namespace POS.Application.Interfaces.Platform;

public interface IPlatformTenantUserAdminService
{
    Task<Result<TenantUserSummaryDto>> BlockAsync(
        string tenantId,
        string userId,
        string justification,
        CancellationToken cancellationToken = default);

    Task<Result<TenantUserSummaryDto>> UnblockAsync(
        string tenantId,
        string userId,
        string justification,
        CancellationToken cancellationToken = default);

    Task<Result<PlatformMutationAckDto>> RequestPasswordResetAsync(
        string tenantId,
        string userId,
        string justification,
        CancellationToken cancellationToken = default);

    Task<Result<PlatformMutationAckDto>> ResendEmailConfirmationAsync(
        string tenantId,
        string userId,
        string justification,
        CancellationToken cancellationToken = default);
}
