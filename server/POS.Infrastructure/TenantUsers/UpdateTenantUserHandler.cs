using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.TenantUsers;
using POS.Application.Interfaces;
using POS.Application.TenantUsers;
using POS.Domain.Entities;
using POS.Domain.Tenant;

namespace POS.Infrastructure.TenantUsers;

public sealed class UpdateTenantUserHandler : IUpdateTenantUserHandler
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICurrentUserService _current;
    private readonly IValidator<UpdateTenantUserCommand> _validator;

    public UpdateTenantUserHandler(
        UserManager<ApplicationUser> users,
        ICurrentUserService current,
        IValidator<UpdateTenantUserCommand> validator)
    {
        _users = users;
        _current = current;
        _validator = validator;
    }

    public async Task<Result<TenantUserListItemDto>> HandleAsync(
        UpdateTenantUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var v = await _validator.ValidateAsync(command, cancellationToken);
        if (!v.IsValid)
        {
            return Result<TenantUserListItemDto>.Failure(
                "tenant.users.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        var tenantId = _current.TenantId?.Trim();
        if (string.IsNullOrEmpty(tenantId))
        {
            return Result<TenantUserListItemDto>.Failure(
                "tenant.users.tenant_required",
                "No se pudo determinar el negocio.");
        }

        var user = await TenantUserMappings.TenantUsersOf(_users, tenantId)
            .FirstOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null)
            return Result<TenantUserListItemDto>.Failure("tenant.users.not_found", "Usuario no encontrado.");

        var newRole = TenantRoleNames.Normalize(command.Role);
        var currentRoles = (await _users.GetRolesAsync(user)).Where(TenantRoleNames.IsKnownRole).ToList();
        var wasAdmin = currentRoles.Contains(TenantRoleNames.Admin);
        var becomesNonAdmin = newRole != TenantRoleNames.Admin;

        if (wasAdmin && becomesNonAdmin)
        {
            var lastAdmin = await IsLastAdminAsync(tenantId, user.Id, cancellationToken);
            if (lastAdmin)
            {
                return Result<TenantUserListItemDto>.Failure(
                    "tenant.users.last_admin",
                    "No se puede quitar el rol Admin al último administrador del negocio.");
            }
        }

        user.FullName = command.FullName.Trim();
        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded)
        {
            var details = string.Join(" ", update.Errors.Select(e => e.Description));
            return Result<TenantUserListItemDto>.Failure("tenant.users.update_failed", details);
        }

        var toRemove = currentRoles.Where(r => r != newRole).ToList();
        if (toRemove.Count > 0)
            await _users.RemoveFromRolesAsync(user, toRemove);

        if (!currentRoles.Contains(newRole))
        {
            var add = await _users.AddToRoleAsync(user, newRole);
            if (!add.Succeeded)
            {
                var details = string.Join(" ", add.Errors.Select(e => e.Description));
                return Result<TenantUserListItemDto>.Failure("tenant.users.role_failed", details);
            }
        }

        return Result<TenantUserListItemDto>.Ok(await TenantUserMappings.ToDtoAsync(_users, user, cancellationToken));
    }

    private async Task<bool> IsLastAdminAsync(string tenantId, string excludeUserId, CancellationToken cancellationToken)
    {
        var admins = await _users.GetUsersInRoleAsync(TenantRoleNames.Admin);
        return admins.Count(u =>
            u.TenantId == tenantId
            && u.AccountKind == Domain.Platform.UserAccountKind.TenantUser
            && u.Id != excludeUserId) == 0;
    }
}
