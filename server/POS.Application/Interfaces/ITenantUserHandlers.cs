using POS.Application.Common;
using POS.Application.Contracts.TenantUsers;
using POS.Application.TenantUsers;

namespace POS.Application.Interfaces;

public interface ICreateTenantUserHandler
{
    Task<Result<TenantUserListItemDto>> HandleAsync(
        CreateTenantUserCommand command,
        CancellationToken cancellationToken = default);
}

public interface IUpdateTenantUserHandler
{
    Task<Result<TenantUserListItemDto>> HandleAsync(
        UpdateTenantUserCommand command,
        CancellationToken cancellationToken = default);
}

public interface ISetTenantUserBlockedHandler
{
    Task<Result<TenantUserListItemDto>> HandleAsync(
        SetTenantUserBlockedCommand command,
        CancellationToken cancellationToken = default);
}

public interface IListTenantUsersQuery
{
    Task<TenantUserDirectoryPageDto> ListAsync(
        int page,
        int pageSize,
        string? emailContains,
        CancellationToken cancellationToken = default);
}

public interface IRequestTenantUserPasswordResetHandler
{
    Task<Result<object?>> HandleAsync(string userId, CancellationToken cancellationToken = default);
}
