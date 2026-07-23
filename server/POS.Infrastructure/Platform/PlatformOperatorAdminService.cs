using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces;
using POS.Application.Interfaces.Platform;
using POS.Application.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;

namespace POS.Infrastructure.Platform;

public sealed class PlatformOperatorAdminService : IPlatformOperatorAdminService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IProvisionPlatformUserHandler _provision;
    private readonly ICurrentUserService _currentUser;
    private readonly ICurrentUserTenantContext _tenantContext;
    private readonly IPlatformAuditService _audit;
    private readonly IValidator<UpdatePlatformOperatorCommand> _updateValidator;
    private readonly IValidator<BlockPlatformOperatorCommand> _blockValidator;
    private readonly IValidator<UnblockPlatformOperatorCommand> _unblockValidator;

    public PlatformOperatorAdminService(
        UserManager<ApplicationUser> userManager,
        IProvisionPlatformUserHandler provision,
        ICurrentUserService currentUser,
        ICurrentUserTenantContext tenantContext,
        IPlatformAuditService audit,
        IValidator<UpdatePlatformOperatorCommand> updateValidator,
        IValidator<BlockPlatformOperatorCommand> blockValidator,
        IValidator<UnblockPlatformOperatorCommand> unblockValidator)
    {
        _userManager = userManager;
        _provision = provision;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _audit = audit;
        _updateValidator = updateValidator;
        _blockValidator = blockValidator;
        _unblockValidator = unblockValidator;
    }

    public async Task<Result<PlatformOperatorSummaryDto>> ProvisionAsync(
        ProvisionPlatformUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _provision.HandleAsync(command, cancellationToken);
        if (!result.IsSuccess)
            return Result<PlatformOperatorSummaryDto>.Failure(result.ErrorCode!, result.Error!);

        var user = await _userManager.FindByIdAsync(result.Value!.UserId);
        if (user is null)
        {
            return Result<PlatformOperatorSummaryDto>.Failure(
                "platform.operators.not_found",
                "No se encontró el operador recién creado.");
        }

        return Result<PlatformOperatorSummaryDto>.Ok(
            await PlatformOperatorMappings.ToSummaryAsync(_userManager, user));
    }

    public async Task<Result<PlatformOperatorSummaryDto>> UpdateAsync(
        UpdatePlatformOperatorCommand command,
        CancellationToken cancellationToken = default)
    {
        var v = await _updateValidator.ValidateAsync(command, cancellationToken);
        if (!v.IsValid)
        {
            return Result<PlatformOperatorSummaryDto>.Failure(
                "platform.validation",
                string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
        }

        var user = await FindPlatformOperatorAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return Result<PlatformOperatorSummaryDto>.Failure(
                "platform.operators.not_found",
                "No existe el operador de plataforma.");
        }

        var currentRole = await ResolvePlatformRoleAsync(user);
        var newRole = command.PlatformRole.Trim();
        var roleChanging = !string.Equals(currentRole, newRole, StringComparison.Ordinal);

        if (roleChanging && PlatformOperatorRules.IsSelf(_currentUser.UserId, user.Id))
        {
            return Result<PlatformOperatorSummaryDto>.Failure(
                "platform.operators.self_role_change",
                "No podés cambiar tu propio rol de plataforma.");
        }

        if (roleChanging
            && string.Equals(currentRole, PlatformRoleNames.SuperAdmin, StringComparison.Ordinal)
            && !string.Equals(newRole, PlatformRoleNames.SuperAdmin, StringComparison.Ordinal))
        {
            var activeCount = await CountActiveSuperAdminsAsync(cancellationToken);
            if (PlatformOperatorRules.WouldRemoveLastActiveSuperAdmin(
                    targetIsActiveSuperAdmin: !user.BlockedByPlatform,
                    actionRemovesSuperAdminPrivilege: true,
                    activeSuperAdminCount: activeCount))
            {
                return Result<PlatformOperatorSummaryDto>.Failure(
                    "platform.operators.last_super_admin",
                    "No se puede degradar al último SuperAdmin activo.");
            }
        }

        var previous = _tenantContext.OverriddenTenantId;
        try
        {
            _tenantContext.OverriddenTenantId = PlatformScope.ReservedTenantId;

            user.FullName = command.FullName.Trim();
            var update = await _userManager.UpdateAsync(user);
            if (!update.Succeeded)
            {
                return Result<PlatformOperatorSummaryDto>.Failure(
                    "platform.operators.update_failed",
                    string.Join(" ", update.Errors.Select(e => e.Description)));
            }

            if (roleChanging)
            {
                var existing = await _userManager.GetRolesAsync(user);
                var toRemove = existing.Where(PlatformRoleNames.IsKnownRole).ToList();
                if (toRemove.Count > 0)
                {
                    var remove = await _userManager.RemoveFromRolesAsync(user, toRemove);
                    if (!remove.Succeeded)
                    {
                        return Result<PlatformOperatorSummaryDto>.Failure(
                            "platform.operators.role_change_failed",
                            string.Join(" ", remove.Errors.Select(e => e.Description)));
                    }
                }

                var add = await _userManager.AddToRoleAsync(user, newRole);
                if (!add.Succeeded)
                {
                    return Result<PlatformOperatorSummaryDto>.Failure(
                        "platform.operators.role_change_failed",
                        string.Join(" ", add.Errors.Select(e => e.Description)));
                }
            }
        }
        finally
        {
            _tenantContext.OverriddenTenantId = previous;
        }

        await _audit.LogAsync(
            new PlatformAuditEventData(
                Action: "PlatformUserUpdated",
                ResourceType: nameof(ApplicationUser),
                ResourceId: user.Id,
                Details: $"fullName={user.FullName}; role={currentRole}->{newRole}"),
            cancellationToken);

        return Result<PlatformOperatorSummaryDto>.Ok(
            await PlatformOperatorMappings.ToSummaryAsync(_userManager, user));
    }

    public Task<Result<PlatformOperatorSummaryDto>> BlockAsync(
        BlockPlatformOperatorCommand command,
        CancellationToken cancellationToken = default) =>
        SetBlockedAsync(command.UserId, command.Justification, blocked: true, cancellationToken);

    public Task<Result<PlatformOperatorSummaryDto>> UnblockAsync(
        UnblockPlatformOperatorCommand command,
        CancellationToken cancellationToken = default) =>
        SetBlockedAsync(command.UserId, command.Justification, blocked: false, cancellationToken);

    private async Task<Result<PlatformOperatorSummaryDto>> SetBlockedAsync(
        string userId,
        string justification,
        bool blocked,
        CancellationToken cancellationToken)
    {
        if (blocked)
        {
            var v = await _blockValidator.ValidateAsync(
                new BlockPlatformOperatorCommand(userId, justification),
                cancellationToken);
            if (!v.IsValid)
            {
                return Result<PlatformOperatorSummaryDto>.Failure(
                    "platform.validation",
                    string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
            }
        }
        else
        {
            var v = await _unblockValidator.ValidateAsync(
                new UnblockPlatformOperatorCommand(userId, justification),
                cancellationToken);
            if (!v.IsValid)
            {
                return Result<PlatformOperatorSummaryDto>.Failure(
                    "platform.validation",
                    string.Join(" ", v.Errors.Select(e => e.ErrorMessage)));
            }
        }

        var user = await FindPlatformOperatorAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result<PlatformOperatorSummaryDto>.Failure(
                "platform.operators.not_found",
                "No existe el operador de plataforma.");
        }

        if (PlatformOperatorRules.IsSelf(_currentUser.UserId, user.Id))
        {
            return Result<PlatformOperatorSummaryDto>.Failure(
                "platform.operators.self_block",
                "No podés bloquear o desbloquear tu propia cuenta.");
        }

        if (blocked && user.BlockedByPlatform)
            return Result<PlatformOperatorSummaryDto>.Ok(await PlatformOperatorMappings.ToSummaryAsync(_userManager, user));

        if (!blocked && !user.BlockedByPlatform)
            return Result<PlatformOperatorSummaryDto>.Ok(await PlatformOperatorMappings.ToSummaryAsync(_userManager, user));

        if (blocked)
        {
            var role = await ResolvePlatformRoleAsync(user);
            if (string.Equals(role, PlatformRoleNames.SuperAdmin, StringComparison.Ordinal))
            {
                var activeCount = await CountActiveSuperAdminsAsync(cancellationToken);
                if (PlatformOperatorRules.WouldRemoveLastActiveSuperAdmin(
                        targetIsActiveSuperAdmin: !user.BlockedByPlatform,
                        actionRemovesSuperAdminPrivilege: true,
                        activeSuperAdminCount: activeCount))
                {
                    return Result<PlatformOperatorSummaryDto>.Failure(
                        "platform.operators.last_super_admin",
                        "No se puede bloquear al último SuperAdmin activo.");
                }
            }
        }

        var previous = _tenantContext.OverriddenTenantId;
        try
        {
            _tenantContext.OverriddenTenantId = PlatformScope.ReservedTenantId;
            user.BlockedByPlatform = blocked;
            var update = await _userManager.UpdateAsync(user);
            if (!update.Succeeded)
            {
                return Result<PlatformOperatorSummaryDto>.Failure(
                    "platform.operators.update_failed",
                    string.Join(" ", update.Errors.Select(e => e.Description)));
            }
        }
        finally
        {
            _tenantContext.OverriddenTenantId = previous;
        }

        var action = blocked ? "PlatformUserBlocked" : "PlatformUserUnblocked";
        await _audit.LogAsync(
            new PlatformAuditEventData(
                Action: action,
                ResourceType: nameof(ApplicationUser),
                ResourceId: user.Id,
                Details: $"email={user.Email}",
                Justification: justification.Trim()),
            cancellationToken);

        return Result<PlatformOperatorSummaryDto>.Ok(
            await PlatformOperatorMappings.ToSummaryAsync(_userManager, user));
    }

    private async Task<ApplicationUser?> FindPlatformOperatorAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return await _userManager.Users.FirstOrDefaultAsync(
            u =>
                u.Id == userId
                && u.AccountKind == UserAccountKind.PlatformUser
                && u.TenantId == PlatformScope.ReservedTenantId,
            cancellationToken);
    }

    private async Task<string> ResolvePlatformRoleAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return roles.FirstOrDefault(PlatformRoleNames.IsKnownRole) ?? string.Empty;
    }

    private async Task<int> CountActiveSuperAdminsAsync(CancellationToken cancellationToken)
    {
        var inRole = await _userManager.GetUsersInRoleAsync(PlatformRoleNames.SuperAdmin);
        return inRole.Count(u =>
            u.AccountKind == UserAccountKind.PlatformUser
            && u.TenantId == PlatformScope.ReservedTenantId
            && !u.BlockedByPlatform);
    }
}
