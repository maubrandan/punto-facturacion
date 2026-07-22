using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.TenantUsers;
using POS.Application.Interfaces;
using POS.Application.TenantUsers;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Domain.Tenant;

namespace POS.Infrastructure.TenantUsers;

public sealed class CreateTenantUserHandler : ICreateTenantUserHandler
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICurrentUserService _current;
    private readonly ITenantEntitlementGuard _entitlements;
    private readonly IValidator<CreateTenantUserCommand> _validator;

    public CreateTenantUserHandler(
        UserManager<ApplicationUser> users,
        ICurrentUserService current,
        ITenantEntitlementGuard entitlements,
        IValidator<CreateTenantUserCommand> validator)
    {
        _users = users;
        _current = current;
        _entitlements = entitlements;
        _validator = validator;
    }

    public async Task<Result<TenantUserListItemDto>> HandleAsync(
        CreateTenantUserCommand command,
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

        var quota = await _entitlements.EnsureCanAddTenantUserAsync(tenantId, cancellationToken);
        if (!quota.IsSuccess)
            return Result<TenantUserListItemDto>.Failure(quota.ErrorCode!, quota.Error!);

        var email = command.Email.Trim();
        if (await _users.FindByEmailAsync(email) is not null)
        {
            return Result<TenantUserListItemDto>.Failure(
                "tenant.users.duplicate",
                "El email ya está registrado.");
        }

        var role = TenantRoleNames.Normalize(command.Role);
        var businessType = await ResolveBusinessTypeAsync(tenantId, cancellationToken);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = string.IsNullOrWhiteSpace(command.FullName) ? email : command.FullName.Trim(),
            TenantId = tenantId,
            BusinessType = businessType,
            AccountKind = UserAccountKind.TenantUser
        };

        var create = await _users.CreateAsync(user, command.Password);
        if (!create.Succeeded)
        {
            var details = string.Join(" ", create.Errors.Select(e => e.Description));
            return Result<TenantUserListItemDto>.Failure("tenant.users.create_failed", details);
        }

        var addRole = await _users.AddToRoleAsync(user, role);
        if (!addRole.Succeeded)
        {
            await _users.DeleteAsync(user);
            var details = string.Join(" ", addRole.Errors.Select(e => e.Description));
            return Result<TenantUserListItemDto>.Failure("tenant.users.role_failed", details);
        }

        return Result<TenantUserListItemDto>.Ok(await TenantUserMappings.ToDtoAsync(_users, user, cancellationToken));
    }

    private async Task<string> ResolveBusinessTypeAsync(string tenantId, CancellationToken cancellationToken)
    {
        var currentId = _current.UserId;
        if (!string.IsNullOrEmpty(currentId))
        {
            var me = await _users.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == currentId, cancellationToken);
            if (me is not null && !string.IsNullOrWhiteSpace(me.BusinessType))
                return me.BusinessType;
        }

        return BusinessTypeNames.Kiosco;
    }
}
