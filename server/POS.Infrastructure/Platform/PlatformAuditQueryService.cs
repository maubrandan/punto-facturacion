using Microsoft.EntityFrameworkCore;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces.Platform;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Platform;

public sealed class PlatformAuditQueryService : IPlatformAuditQueryService
{
    private readonly ApplicationDbContext _db;

    public PlatformAuditQueryService(ApplicationDbContext db) => _db = db;

    public async Task<PlatformAuditEventPageDto> GetPageAsync(
        int page,
        int pageSize,
        PlatformAuditListFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(_db.PlatformAuditEvents.AsNoTracking(), filter);
        var total = await query.CountAsync(cancellationToken);
        var skip = (page - 1) * pageSize;
        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new PlatformAuditEventDto
            {
                Id = x.Id,
                CreatedAtUtc = x.CreatedAtUtc,
                Action = x.Action,
                ActorUserId = x.ActorUserId,
                ActorEmail = x.ActorEmail,
                ResourceType = x.ResourceType,
                ResourceId = x.ResourceId,
                AffectedTenantId = x.AffectedTenantId,
                Details = x.Details,
                Justification = x.Justification,
                CorrelationId = x.CorrelationId,
                IpAddress = x.IpAddress,
                IsImpersonationContext = x.IsImpersonationContext
            })
            .ToListAsync(cancellationToken);

        return new PlatformAuditEventPageDto
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static IQueryable<PlatformAuditEvent> ApplyFilter(
        IQueryable<PlatformAuditEvent> query,
        PlatformAuditListFilter? filter)
    {
        if (filter is null)
            return query;

        if (!string.IsNullOrWhiteSpace(filter.TenantId))
        {
            var tenantId = filter.TenantId.Trim();
            query = query.Where(x => x.AffectedTenantId == tenantId);
        }

        if (!string.IsNullOrWhiteSpace(filter.ActorUserId))
        {
            var actor = filter.ActorUserId.Trim();
            query = query.Where(x => x.ActorUserId == actor);
        }

        if (filter.CreatedFromUtc is { } from)
            query = query.Where(x => x.CreatedAtUtc >= from);

        if (filter.CreatedToUtc is { } to)
            query = query.Where(x => x.CreatedAtUtc <= to);

        return query;
    }
}
