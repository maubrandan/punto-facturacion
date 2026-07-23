using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
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
    private const int MaxTopSkusTake = 50;
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
                s => new
                {
                    s.Id,
                    s.Date,
                    s.TotalNet,
                    s.TotalTax,
                    s.TotalAmount,
                    s.CreatedByUserName,
                    s.TenantId,
                    Lines = s.Details
                        .Select(
                            d => new
                            {
                                d.Id,
                                d.ProductId,
                                d.ProductName,
                                d.ProductExtendedDataJson,
                                d.Quantity,
                                d.LineNetSubtotal,
                                d.LineTaxAmount,
                                d.StockLotId
                            })
                        .ToList(),
                    Payments = s.Payments
                        .OrderBy(p => p.CreatedAt)
                        .ThenBy(p => p.Id)
                        .Select(
                            p => new SalePaymentResponse
                            {
                                Id = p.Id,
                                Method = (int)p.Method,
                                Amount = p.Amount
                            })
                        .ToList()
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (sale is null)
            return null;

        var lotIds = sale.Lines
            .Where(l => l.StockLotId.HasValue)
            .Select(l => l.StockLotId!.Value)
            .Distinct()
            .ToList();

        var lotsById = lotIds.Count == 0
            ? new Dictionary<Guid, StockLot>()
            : await _db.Set<StockLot>()
                .AsNoTracking()
                .Where(l => lotIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, cancellationToken);

        var profileTaxId = await _db.Set<TenantFiscalProfile>()
            .AsNoTracking()
            .Where(p => p.TenantId == sale.TenantId)
            .Select(p => p.TaxId)
            .FirstOrDefaultAsync(cancellationToken);

        var fiscalDocuments = await _db.Set<FiscalDocument>()
            .AsNoTracking()
            .Where(d => d.SaleId == id && d.TenantId == sale.TenantId)
            .OrderBy(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        // FEFO: líneas con lote por vencimiento asc.; resto estable por producto/id.
        var orderedLines = sale.Lines
            .OrderBy(d => d.StockLotId is Guid lid && lotsById.TryGetValue(lid, out var lot)
                ? lot.ExpirationDate
                : DateOnly.MaxValue)
            .ThenBy(d => d.StockLotId is Guid lid && lotsById.TryGetValue(lid, out var lot)
                ? lot.LotNumber
                : string.Empty)
            .ThenBy(d => d.ProductName)
            .ThenBy(d => d.Id)
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
                    LineTotal = d.LineNetSubtotal + d.LineTaxAmount,
                    StockLotId = d.StockLotId,
                    LotNumber = d.StockLotId is Guid lotId && lotsById.TryGetValue(lotId, out var stockLot)
                        ? stockLot.LotNumber
                        : null
                })
            .ToList();

        return new SaleDetailViewResponse
        {
            Id = sale.Id,
            Date = sale.Date,
            TotalNet = sale.TotalNet,
            TotalTax = sale.TotalTax,
            TotalAmount = sale.TotalAmount,
            CreatedByUserName = sale.CreatedByUserName,
            Lines = orderedLines,
            Payments = sale.Payments,
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

    public async Task<SalesReportResponse> GetSalesReportAsync(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var startDay = (startDate ?? endDate ?? today).Date;
        var endDay = (endDate ?? startDate ?? today).Date;
        if (endDay < startDay)
            (startDay, endDay) = (endDay, startDay);

        var start = DateTime.SpecifyKind(startDay, DateTimeKind.Utc);
        var endExclusive = DateTime.SpecifyKind(endDay, DateTimeKind.Utc).AddDays(1);

        var salesQ = _db.Sales.AsNoTracking().Where(s => s.Date >= start && s.Date < endExclusive);

        var totalSalesAmount = await salesQ.SumAsync(s => (decimal?)s.TotalAmount, cancellationToken) ?? 0m;
        var salesCount = await salesQ.CountAsync(cancellationToken);

        var byPayment = await (
                from p in _db.SalePayments.AsNoTracking()
                join s in _db.Sales.AsNoTracking() on p.SaleId equals s.Id
                where s.Date >= start && s.Date < endExclusive
                group p by p.Method
                into g
                select new SalesReportPaymentBreakdownItem
                {
                    Method = (int)g.Key,
                    Amount = g.Sum(x => x.Amount),
                    PaymentCount = g.Count()
                })
            .OrderBy(x => x.Method)
            .ToListAsync(cancellationToken);

        var byCashierRaw = await salesQ
            .GroupBy(s => new { s.CreatedByUserId, s.CreatedByUserName })
            .Select(
                g => new
                {
                    g.Key.CreatedByUserId,
                    g.Key.CreatedByUserName,
                    TotalAmount = g.Sum(x => x.TotalAmount),
                    SalesCount = g.Count()
                })
            .OrderByDescending(x => x.TotalAmount)
            .ThenBy(x => x.CreatedByUserName)
            .ToListAsync(cancellationToken);

        var byCashier = byCashierRaw
            .Select(
                x => new SalesReportCashierBreakdownItem
                {
                    CreatedByUserId = x.CreatedByUserId,
                    CreatedByUserName = string.IsNullOrWhiteSpace(x.CreatedByUserName)
                        ? "—"
                        : x.CreatedByUserName!,
                    TotalAmount = x.TotalAmount,
                    SalesCount = x.SalesCount
                })
            .ToList();

        return new SalesReportResponse
        {
            StartDate = start,
            EndDate = DateTime.SpecifyKind(endDay, DateTimeKind.Utc),
            TotalSalesAmount = totalSalesAmount,
            SalesCount = salesCount,
            ByPaymentMethod = byPayment,
            ByCashier = byCashier
        };
    }

    public async Task<Result<MarginReportResponse>> GetMarginReportAsync(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var (start, endExclusive, endDay) = NormalizeUtcDayRange(startDate, endDate);

        var lines = await (
                from d in _db.SaleDetails.AsNoTracking()
                join s in _db.Sales.AsNoTracking() on d.SaleId equals s.Id
                join p in _db.Products.AsNoTracking() on d.ProductId equals p.Id into pj
                from p in pj.DefaultIfEmpty()
                where s.Date >= start && s.Date < endExclusive
                select new
                {
                    d.ProductId,
                    d.ProductName,
                    d.Quantity,
                    d.LineNetSubtotal,
                    Sku = p != null ? p.SKU : string.Empty,
                    LastCost = p != null ? p.LastCost : null
                })
            .ToListAsync(cancellationToken);

        var revenueNet = lines.Sum(x => x.LineNetSubtotal);
        var withCost = lines.Where(x => x.LastCost.HasValue).ToList();
        var withoutCost = lines.Where(x => !x.LastCost.HasValue).ToList();
        var revenueWithCost = withCost.Sum(x => x.LineNetSubtotal);
        var revenueWithoutCost = withoutCost.Sum(x => x.LineNetSubtotal);
        var costNet = withCost.Sum(x => x.Quantity * x.LastCost!.Value);
        var marginNet = revenueWithCost - costNet;

        var bySku = lines
            .GroupBy(x => x.ProductId)
            .Select(
                g =>
                {
                    var first = g.First();
                    var hasCost = g.All(x => x.LastCost.HasValue);
                    var qty = g.Sum(x => x.Quantity);
                    var rev = g.Sum(x => x.LineNetSubtotal);
                    decimal? cost = hasCost ? g.Sum(x => x.Quantity * x.LastCost!.Value) : null;
                    return new MarginReportSkuItem
                    {
                        ProductId = g.Key,
                        Sku = string.IsNullOrWhiteSpace(first.Sku) ? "—" : first.Sku,
                        ProductName = first.ProductName,
                        Quantity = qty,
                        RevenueNet = rev,
                        CostNet = cost,
                        MarginNet = cost.HasValue ? rev - cost.Value : null,
                        HasCost = hasCost
                    };
                })
            .OrderByDescending(x => x.RevenueNet)
            .ThenBy(x => x.ProductName)
            .ToList();

        return Result<MarginReportResponse>.Ok(
            new MarginReportResponse
            {
                StartDate = start,
                EndDate = DateTime.SpecifyKind(endDay, DateTimeKind.Utc),
                RevenueNet = revenueNet,
                RevenueNetWithCost = revenueWithCost,
                RevenueNetWithoutCost = revenueWithoutCost,
                CostNet = costNet,
                MarginNet = marginNet,
                LinesWithCost = withCost.Count,
                LinesWithoutCost = withoutCost.Count,
                BySku = bySku
            });
    }

    public async Task<Result<TopSkusReportResponse>> GetTopSkusReportAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? sortBy,
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedSort = string.IsNullOrWhiteSpace(sortBy)
            ? "quantity"
            : sortBy.Trim().ToLowerInvariant();
        if (normalizedSort is not ("quantity" or "revenue"))
        {
            return Result<TopSkusReportResponse>.Failure(
                "sales.report.invalid_sort",
                "sortBy debe ser 'quantity' o 'revenue'.");
        }

        take = take < 1 ? 10 : Math.Min(take, MaxTopSkusTake);
        var (start, endExclusive, endDay) = NormalizeUtcDayRange(startDate, endDate);

        var lines = await (
                from d in _db.SaleDetails.AsNoTracking()
                join s in _db.Sales.AsNoTracking() on d.SaleId equals s.Id
                where s.Date >= start && s.Date < endExclusive
                select new
                {
                    d.ProductId,
                    d.ProductName,
                    d.Quantity,
                    d.LineNetSubtotal,
                    d.LineTaxAmount
                })
            .ToListAsync(cancellationToken);

        var productIds = lines.Select(x => x.ProductId).Distinct().ToList();
        var skusByProduct = productIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.SKU })
                .ToDictionaryAsync(p => p.Id, p => p.SKU, cancellationToken);

        var grouped = lines
            .GroupBy(x => x.ProductId)
            .Select(
                g =>
                {
                    var name = g.Min(x => x.ProductName) ?? "—";
                    skusByProduct.TryGetValue(g.Key, out var sku);
                    return new TopSkuReportItem
                    {
                        ProductId = g.Key,
                        Sku = string.IsNullOrWhiteSpace(sku) ? "—" : sku,
                        ProductName = name,
                        Quantity = g.Sum(x => x.Quantity),
                        RevenueNet = g.Sum(x => x.LineNetSubtotal),
                        RevenueTotal = g.Sum(x => x.LineNetSubtotal + x.LineTaxAmount)
                    };
                });

        var ordered = normalizedSort == "revenue"
            ? grouped.OrderByDescending(x => x.RevenueNet).ThenBy(x => x.ProductName)
            : grouped.OrderByDescending(x => x.Quantity).ThenBy(x => x.ProductName);

        var items = ordered.Take(take).ToList();

        return Result<TopSkusReportResponse>.Ok(
            new TopSkusReportResponse
            {
                StartDate = start,
                EndDate = DateTime.SpecifyKind(endDay, DateTimeKind.Utc),
                SortBy = normalizedSort,
                Items = items
            });
    }

    public async Task<Result<SalesByPeriodReportResponse>> GetSalesByPeriodReportAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? period,
        CancellationToken cancellationToken = default)
    {
        var normalizedPeriod = string.IsNullOrWhiteSpace(period)
            ? "day"
            : period.Trim().ToLowerInvariant();
        if (normalizedPeriod is not ("day" or "week" or "month"))
        {
            return Result<SalesByPeriodReportResponse>.Failure(
                "sales.report.invalid_period",
                "period debe ser 'day', 'week' o 'month'.");
        }

        var (start, endExclusive, endDay) = NormalizeUtcDayRange(startDate, endDate);
        var salesQ = _db.Sales.AsNoTracking().Where(s => s.Date >= start && s.Date < endExclusive);

        var totalSalesAmount = await salesQ.SumAsync(s => (decimal?)s.TotalAmount, cancellationToken) ?? 0m;
        var salesCount = await salesQ.CountAsync(cancellationToken);

        var daily = await salesQ
            .GroupBy(s => s.Date.Date)
            .Select(
                g => new
                {
                    Day = g.Key,
                    TotalSalesAmount = g.Sum(x => x.TotalAmount),
                    SalesCount = g.Count()
                })
            .OrderBy(x => x.Day)
            .ToListAsync(cancellationToken);

        IReadOnlyList<SalesByPeriodBucketItem> buckets = normalizedPeriod switch
        {
            "week" => daily
                .GroupBy(x => StartOfUtcWeekMonday(x.Day))
                .Select(
                    g =>
                    {
                        var periodStart = g.Key;
                        var periodEnd = periodStart.AddDays(6);
                        return new SalesByPeriodBucketItem
                        {
                            PeriodStart = periodStart,
                            PeriodEnd = periodEnd,
                            TotalSalesAmount = g.Sum(x => x.TotalSalesAmount),
                            SalesCount = g.Sum(x => x.SalesCount)
                        };
                    })
                .OrderBy(x => x.PeriodStart)
                .ToList(),
            "month" => daily
                .GroupBy(x => new DateTime(x.Day.Year, x.Day.Month, 1, 0, 0, 0, DateTimeKind.Utc))
                .Select(
                    g =>
                    {
                        var periodStart = g.Key;
                        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
                        return new SalesByPeriodBucketItem
                        {
                            PeriodStart = periodStart,
                            PeriodEnd = periodEnd,
                            TotalSalesAmount = g.Sum(x => x.TotalSalesAmount),
                            SalesCount = g.Sum(x => x.SalesCount)
                        };
                    })
                .OrderBy(x => x.PeriodStart)
                .ToList(),
            _ => daily
                .Select(
                    x =>
                    {
                        var day = DateTime.SpecifyKind(x.Day.Date, DateTimeKind.Utc);
                        return new SalesByPeriodBucketItem
                        {
                            PeriodStart = day,
                            PeriodEnd = day,
                            TotalSalesAmount = x.TotalSalesAmount,
                            SalesCount = x.SalesCount
                        };
                    })
                .ToList()
        };

        return Result<SalesByPeriodReportResponse>.Ok(
            new SalesByPeriodReportResponse
            {
                StartDate = start,
                EndDate = DateTime.SpecifyKind(endDay, DateTimeKind.Utc),
                Period = normalizedPeriod,
                TotalSalesAmount = totalSalesAmount,
                SalesCount = salesCount,
                Buckets = buckets
            });
    }

    private static (DateTime Start, DateTime EndExclusive, DateTime EndDay) NormalizeUtcDayRange(
        DateTime? startDate,
        DateTime? endDate)
    {
        var today = DateTime.UtcNow.Date;
        var startDay = (startDate ?? endDate ?? today).Date;
        var endDay = (endDate ?? startDate ?? today).Date;
        if (endDay < startDay)
            (startDay, endDay) = (endDay, startDay);

        var start = DateTime.SpecifyKind(startDay, DateTimeKind.Utc);
        var endExclusive = DateTime.SpecifyKind(endDay, DateTimeKind.Utc).AddDays(1);
        return (start, endExclusive, endDay);
    }

    /// <summary>Inicio de semana UTC con lunes como primer día.</summary>
    private static DateTime StartOfUtcWeekMonday(DateTime day)
    {
        var d = day.Date;
        var diff = ((int)d.DayOfWeek + 6) % 7;
        return DateTime.SpecifyKind(d.AddDays(-diff), DateTimeKind.Utc);
    }
}
