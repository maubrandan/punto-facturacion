using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POS.Application.Contracts.TenantUsers;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Tenant;

namespace POS.Infrastructure.TenantUsers;

public sealed class ListTenantUsersQuery : IListTenantUsersQuery
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ICurrentUserService _current;

    public ListTenantUsersQuery(UserManager<ApplicationUser> users, ICurrentUserService current)
    {
        _users = users;
        _current = current;
    }

    public async Task<TenantUserDirectoryPageDto> ListAsync(
        int page,
        int pageSize,
        string? emailContains,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var tenantId = _current.TenantId?.Trim() ?? string.Empty;
        var query = TenantUserMappings.TenantUsersOf(_users, tenantId).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(emailContains))
        {
            var filter = emailContains.Trim().ToLowerInvariant();
            query = query.Where(u => u.Email != null && u.Email.ToLower().Contains(filter));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<TenantUserListItemDto>();
        foreach (var u in rows)
            items.Add(await TenantUserMappings.ToDtoAsync(_users, u, cancellationToken));

        return new TenantUserDirectoryPageDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }
}
