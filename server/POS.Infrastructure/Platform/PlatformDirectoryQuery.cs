using Microsoft.EntityFrameworkCore;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Platform;

public sealed class PlatformDirectoryQuery : IPlatformDirectoryQuery
{
    private readonly ApplicationDbContext _db;

    public PlatformDirectoryQuery(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TenantSummaryDto>> ListAllTenantsAsync(
        CancellationToken cancellationToken = default)
    {
        var list = await _db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TenantSummaryDto
            {
                Id = t.Id,
                Name = t.Name,
                ContactEmail = t.ContactEmail,
                Status = t.Status,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<TenantDirectoryPageDto> ListTenantsPageAsync(
        int page,
        int pageSize,
        TenantListFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var q = ApplyFilter(_db.Tenants.AsNoTracking(), filter);
        var total = await q.CountAsync(cancellationToken);
        var skip = (page - 1) * pageSize;
        var items = await q
            .OrderBy(t => t.Name)
            .Skip(skip)
            .Take(pageSize)
            .Select(t => new TenantSummaryDto
            {
                Id = t.Id,
                Name = t.Name,
                ContactEmail = t.ContactEmail,
                Status = t.Status,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new TenantDirectoryPageDto
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<TenantDetailDto?> GetTenantByIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var t = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        return t is null ? null : TenantMappings.ToDetailDto(t);
    }

    private static IQueryable<Tenant> ApplyFilter(IQueryable<Tenant> query, TenantListFilter? filter)
    {
        if (filter is null)
            return query;

        if (!string.IsNullOrWhiteSpace(filter.NameContains))
        {
            var s = filter.NameContains.Trim();
            query = query.Where(t => t.Name.Contains(s));
        }

        if (filter.Status is { } status)
            query = query.Where(t => t.Status == status);

        if (filter.CreatedFromUtc is { } from)
            query = query.Where(t => t.CreatedAt >= from);

        if (filter.CreatedToUtc is { } to)
            query = query.Where(t => t.CreatedAt <= to);

        return query;
    }
}
