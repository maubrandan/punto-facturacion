using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POS.Application.Contracts.TenantUsers;
using POS.Domain.Entities;
using POS.Domain.Platform;
using POS.Domain.Tenant;

namespace POS.Infrastructure.TenantUsers;

internal static class TenantUserMappings
{
    public static async Task<TenantUserListItemDto> ToDtoAsync(
        UserManager<ApplicationUser> users,
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var roles = await users.GetRolesAsync(user);
        var role = roles.FirstOrDefault(TenantRoleNames.IsKnownRole) ?? string.Empty;
        return new TenantUserListItemDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Role = role,
            EmailConfirmed = user.EmailConfirmed,
            BlockedByTenant = user.BlockedByTenant,
            BlockedByPlatform = user.BlockedByPlatform,
            LockoutEnabled = user.LockoutEnabled,
            LockoutEnd = user.LockoutEnd
        };
    }

    public static IQueryable<ApplicationUser> TenantUsersOf(
        UserManager<ApplicationUser> users,
        string tenantId) =>
        users.Users.Where(u =>
            u.TenantId == tenantId && u.AccountKind == UserAccountKind.TenantUser);
}
