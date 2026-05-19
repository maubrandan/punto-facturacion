using Microsoft.EntityFrameworkCore;
using POS.Domain.Common;

namespace POS.Infrastructure.Platform;

/// <summary>
/// Uso **solo** en consultas de operador de plataforma cuando la entidad implementa
/// <see cref="ITenantEntity"/> y tiene filtro global por <c>TenantId</c>. Ver <c>docs/PLATFORM-QUERY-FILTERS.md</c>.
/// </summary>
public static class PlatformEfQueryExtensions
{
    public static IQueryable<TEntity> FilterByTenant<TEntity>(
        this IQueryable<TEntity> source,
        string tenantId)
        where TEntity : class, ITenantEntity
    {
        var id = tenantId.Trim();
        return source.IgnoreQueryFilters().Where(e => e.TenantId == id);
    }
}
