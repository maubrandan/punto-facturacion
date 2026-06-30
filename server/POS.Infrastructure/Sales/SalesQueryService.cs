using Microsoft.EntityFrameworkCore;
using POS.Application.Contracts.Fiscal;
using POS.Application.Contracts.Sales;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Fiscal;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Sales;

public sealed class SalesQueryService : ISalesQueryService
{
    private const int MaxPageSize = 100;
    private readonly ApplicationDbContext _db;

    public SalesQueryService(ApplicationDbContext db) => _db = db;

    public async Task<PagedSalesResponse> GetPagedAsync(
        DateTime? startDate,
        DateTime? endDate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);

        var query = _db.Sales.AsNoTracking();
        if (startDate.HasValue)
        {
            var start = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
            query = query.Where(s => s.Date >= start);
        }

        if (endDate.HasValue)
        {
            var endExclusive = DateTime.SpecifyKind(endDate.Value.Date, DateTimeKind.Utc).AddDays(1);
            query = query.Where(s => s.Date < endExclusive);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(
                s => new SaleSummaryResponse
                {
                    Id = s.Id,
                    Fecha = s.Date,
                    Total = s.TotalAmount,
                    UsuarioNombre = s.CreatedByUserName ?? "—",
                    CantidadItems = s.Details.Count
                })
            .ToListAsync(cancellationToken);

        return new PagedSalesResponse
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<SaleDetailViewResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sale = await _db.Sales
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(
                s => new SaleDetailViewResponse
                {
                    Id = s.Id,
                    Date = s.Date,
                    TotalNet = s.TotalNet,
                    TotalTax = s.TotalTax,
                    TotalAmount = s.TotalAmount,
                    CreatedByUserName = s.CreatedByUserName,
                    Lines = s.Details
                        .OrderBy(d => d.Id)
                        .Select(
                            d => new SaleDetailLineViewResponse
                            {
                                Id = d.Id,
                                ProductId = d.ProductId,
                                ProductName = d.ProductName,
                                ProductExtendedDataJson = d.ProductExtendedDataJson,
                                Quantity = d.Quantity,
                                LineNetSubtotal = d.LineNetSubtotal,
                                LineTaxAmount = d.LineTaxAmount,
                                LineTotal = d.LineNetSubtotal + d.LineTaxAmount
                            })
                        .ToList()
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (sale is null)
            return null;

        var tenantId = await _db.Sales.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => s.TenantId)
            .FirstAsync(cancellationToken);

        var profileTaxId = await _db.Set<TenantFiscalProfile>()
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.TaxId)
            .FirstOrDefaultAsync(cancellationToken);

        var fiscalDocuments = await _db.Set<FiscalDocument>()
            .AsNoTracking()
            .Where(d => d.SaleId == id && d.TenantId == tenantId)
            .OrderBy(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return new SaleDetailViewResponse
        {
            Id = sale.Id,
            Date = sale.Date,
            TotalNet = sale.TotalNet,
            TotalTax = sale.TotalTax,
            TotalAmount = sale.TotalAmount,
            CreatedByUserName = sale.CreatedByUserName,
            Lines = sale.Lines,
            FiscalDocuments = fiscalDocuments
                .Select(d => FiscalDocumentMapper.ToResponse(d, profileTaxId))
                .ToList()
        };
    }

    public async Task<DailySummaryResponse> GetDailySummaryAsync(
        DateTime? dateUtc,
        CancellationToken cancellationToken = default)
    {
        var day = dateUtc ?? DateTime.UtcNow;
        var start = DateTime.SpecifyKind(day.Date, DateTimeKind.Utc);
        var endExclusive = start.AddDays(1);

        var salesQ = _db.Sales.AsNoTracking().Where(s => s.Date >= start && s.Date < endExclusive);
        var total = await salesQ.SumAsync(s => s.TotalAmount, cancellationToken);
        var count = await salesQ.CountAsync(cancellationToken);

        var top = await (
                from d in _db.SaleDetails.AsNoTracking()
                join s in _db.Sales.AsNoTracking() on d.SaleId equals s.Id
                where s.Date >= start && s.Date < endExclusive
                group d by d.ProductId
                into g
                select new
                {
                    ProductId = g.Key,
                    Units = g.Sum(x => x.Quantity),
                    Name = g.Min(x => x.ProductName)
                })
            .OrderByDescending(x => x.Units)
            .ThenBy(x => x.ProductId)
            .FirstOrDefaultAsync(cancellationToken);

        if (count == 0 || top is null)
        {
            return new DailySummaryResponse
            {
                TotalFacturado = total,
                VentasCount = count,
                TopProductId = null,
                TopProductName = null,
                TopProductUnits = 0
            };
        }

        return new DailySummaryResponse
        {
            TotalFacturado = total,
            VentasCount = count,
            TopProductId = top.ProductId,
            TopProductName = top.Name,
            TopProductUnits = top.Units
        };
    }
}
