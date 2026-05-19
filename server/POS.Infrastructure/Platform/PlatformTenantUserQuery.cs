using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Domain.Entities;
using POS.Domain.Platform;

namespace POS.Infrastructure.Platform;

public sealed class PlatformTenantUserQuery : IPlatformTenantUserQuery
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPlatformDirectoryQuery _tenants;

    public PlatformTenantUserQuery(
        UserManager<ApplicationUser> userManager,
        IPlatformDirectoryQuery tenants)
    {
        _userManager = userManager;
        _tenants = tenants;
    }

    public async Task<Result<TenantUserDirectoryPageDto>> ListUsersAsync(
        string tenantId,
        int page,
        int pageSize,
        string? emailContains,
        CancellationToken cancellationToken = default)
    {
        if (await _tenants.GetTenantByIdAsync(tenantId, cancellationToken) is null)
        {
            return Result<TenantUserDirectoryPageDto>.Failure(
                "tenant.not_found",
                "No existe el tenant.");
        }

        var q = _userManager.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.AccountKind == UserAccountKind.TenantUser);

        if (!string.IsNullOrWhiteSpace(emailContains))
        {
            var s = emailContains.Trim();
            q = q.Where(u => u.Email != null && u.Email.Contains(s));
        }

        var total = await q.CountAsync(cancellationToken);
        var skip = (page - 1) * pageSize;
        var items = await q
            .OrderBy(u => u.Email)
            .Skip(skip)
            .Take(pageSize)
            .Select(u => new TenantUserSummaryDto
            {
                Id = u.Id,
                Email = u.Email ?? string.Empty,
                FullName = u.FullName,
                EmailConfirmed = u.EmailConfirmed,
                LockoutEnabled = u.LockoutEnabled,
                LockoutEnd = u.LockoutEnd,
                BlockedByPlatform = u.BlockedByPlatform
            })
            .ToListAsync(cancellationToken);

        return Result<TenantUserDirectoryPageDto>.Ok(
            new TenantUserDirectoryPageDto
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            });
    }
}
