using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;

namespace POS.Infrastructure.Platform;

public sealed class PlatformOperatorQuery : IPlatformOperatorQuery
{
    private readonly UserManager<ApplicationUser> _userManager;

    public PlatformOperatorQuery(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<PlatformOperatorDirectoryPageDto> ListAsync(
        int page,
        int pageSize,
        string? emailContains,
        string? role,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ApplicationUser> q = _userManager.Users.AsNoTracking()
            .Where(u =>
                u.AccountKind == UserAccountKind.PlatformUser
                && u.TenantId == PlatformScope.ReservedTenantId);

        if (!string.IsNullOrWhiteSpace(emailContains))
        {
            var s = emailContains.Trim();
            q = q.Where(u => u.Email != null && u.Email.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(role) && PlatformRoleNames.IsKnownRole(role))
        {
            var roleName = role.Trim();
            var inRole = await _userManager.GetUsersInRoleAsync(roleName);
            var ids = inRole
                .Where(u =>
                    u.AccountKind == UserAccountKind.PlatformUser
                    && u.TenantId == PlatformScope.ReservedTenantId)
                .Select(u => u.Id)
                .ToHashSet(StringComparer.Ordinal);
            q = q.Where(u => ids.Contains(u.Id));
        }

        var total = await q.CountAsync(cancellationToken);
        var skip = (page - 1) * pageSize;
        var pageUsers = await q
            .OrderBy(u => u.Email)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<PlatformOperatorSummaryDto>(pageUsers.Count);
        foreach (var user in pageUsers)
        {
            items.Add(await PlatformOperatorMappings.ToSummaryAsync(_userManager, user));
        }

        return new PlatformOperatorDirectoryPageDto
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
