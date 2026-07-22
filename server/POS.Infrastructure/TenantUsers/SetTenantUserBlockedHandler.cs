using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.TenantUsers;
using POS.Application.Interfaces;
using POS.Application.TenantUsers;
using POS.Domain.Entities;
using POS.Domain.Tenant;

namespace POS.Infrastructure.TenantUsers;

public sealed class SetTenantUserBlockedHandler : ISetTenantUserBlockedHandler
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICurrentUserService _current;

    public SetTenantUserBlockedHandler(UserManager<ApplicationUser> users, ICurrentUserService current)
    {
        _users = users;
        _current = current;
    }

    public async Task<Result<TenantUserListItemDto>> HandleAsync(
        SetTenantUserBlockedCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _current.TenantId?.Trim();
        if (string.IsNullOrEmpty(tenantId))
        {
            return Result<TenantUserListItemDto>.Failure(
                "tenant.users.tenant_required",
                "No se pudo determinar el negocio.");
        }

        if (string.Equals(_current.UserId, command.UserId, StringComparison.Ordinal)
            && command.Blocked)
        {
            return Result<TenantUserListItemDto>.Failure(
                "tenant.users.self_block",
                "No podés bloquear tu propia cuenta.");
        }

        var user = await TenantUserMappings.TenantUsersOf(_users, tenantId)
            .FirstOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
            return Result<TenantUserListItemDto>.Failure("tenant.users.not_found", "Usuario no encontrado.");

        if (command.Blocked)
        {
            var roles = await _users.GetRolesAsync(user);
            if (roles.Contains(TenantRoleNames.Admin))
            {
                var otherAdmins = (await _users.GetUsersInRoleAsync(TenantRoleNames.Admin))
                    .Count(u =>
                        u.TenantId == tenantId
                        && u.Id != user.Id
                        && !u.BlockedByTenant
                        && !u.BlockedByPlatform);
                if (otherAdmins == 0)
                {
                    return Result<TenantUserListItemDto>.Failure(
                        "tenant.users.last_admin",
                        "No se puede bloquear al último administrador activo del negocio.");
                }
            }
        }

        user.BlockedByTenant = command.Blocked;
        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded)
        {
            var details = string.Join(" ", update.Errors.Select(e => e.Description));
            return Result<TenantUserListItemDto>.Failure("tenant.users.update_failed", details);
        }

        return Result<TenantUserListItemDto>.Ok(await TenantUserMappings.ToDtoAsync(_users, user, cancellationToken));
    }
}
