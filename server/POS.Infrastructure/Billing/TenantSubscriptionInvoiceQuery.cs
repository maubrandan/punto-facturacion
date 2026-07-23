using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Contracts.Platform;
using POS.Application.Interfaces;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Billing;

public sealed class TenantSubscriptionInvoiceQuery : ITenantSubscriptionInvoiceQuery
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public TenantSubscriptionInvoiceQuery(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<SubscriptionInvoiceListDto>> ListForCurrentTenantAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId?.Trim();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<SubscriptionInvoiceListDto>.Failure(
                "subscription.tenant_required",
                "Se requiere un tenant en el contexto.");
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.SubscriptionInvoices
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(i => i.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Result<SubscriptionInvoiceListDto>.Ok(
            new SubscriptionInvoiceListDto
            {
                Items = items.Select(SubscriptionInvoiceMapper.ToDto).ToList(),
                TotalCount = total
            });
    }

    public async Task<Result<SubscriptionInvoiceDto>> GetForCurrentTenantAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId?.Trim();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<SubscriptionInvoiceDto>.Failure(
                "subscription.tenant_required",
                "Se requiere un tenant en el contexto.");
        }

        var row = await _db.SubscriptionInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == tenantId, cancellationToken);

        if (row is null)
        {
            return Result<SubscriptionInvoiceDto>.Failure(
                "invoice.not_found",
                "No existe la factura o no pertenece a este tenant.");
        }

        return Result<SubscriptionInvoiceDto>.Ok(SubscriptionInvoiceMapper.ToDto(row));
    }
}
