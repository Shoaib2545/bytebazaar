using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using ByteBazaar.Domain;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

/// <summary>
/// Sales reports. Scope: orders with Status != Cancelled, filtered on order CreatedAt (UTC);
/// <c>from</c> is inclusive from midnight, <c>to</c> is inclusive through end of that day.
/// </summary>
public class ReportService
{
    private readonly IAppDbContext _db;

    public ReportService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<List<SalesReportRowDto>> GetSalesAsync(
        DateTime? from, DateTime? to, string? groupBy, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(groupBy) && !groupBy.Equals("day", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException($"Unknown groupBy \"{groupBy}\"; only \"day\" is supported.");

        var orders = ScopedOrders(from, to);
        var rows = await orders
            .Select(o => new { o.CreatedAt, o.Total })
            .ToListAsync(ct);

        return rows
            .GroupBy(o => o.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new SalesReportRowDto
            {
                Period = g.Key.ToString("yyyy-MM-dd"),
                Orders = g.Count(),
                Revenue = g.Sum(o => o.Total)
            })
            .ToList();
    }

    public async Task<List<CategoryReportRowDto>> GetByCategoryAsync(
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var lines = await ScopedLinesAsync(from, to, ct);

        var names = await _db.Categories.AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return lines
            .GroupBy(l => l.CategoryId)
            .Select(g => new CategoryReportRowDto
            {
                CategoryName = g.Key is not null && names.TryGetValue(g.Key.Value, out var name)
                    ? name
                    : "(deleted)",
                Orders = g.Select(l => l.OrderId).Distinct().Count(),
                Units = g.Sum(l => l.Quantity),
                Revenue = g.Sum(l => l.Line)
            })
            .OrderByDescending(r => r.Revenue).ThenBy(r => r.CategoryName)
            .ToList();
    }

    public async Task<List<BrandReportRowDto>> GetByBrandAsync(
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var lines = await ScopedLinesAsync(from, to, ct);

        var names = await _db.Brands.AsNoTracking()
            .ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        return lines
            .GroupBy(l => l.BrandId)
            .Select(g => new BrandReportRowDto
            {
                BrandName = g.Key is not null && names.TryGetValue(g.Key.Value, out var name)
                    ? name
                    : "(no brand)",
                Orders = g.Select(l => l.OrderId).Distinct().Count(),
                Units = g.Sum(l => l.Quantity),
                Revenue = g.Sum(l => l.Line)
            })
            .OrderByDescending(r => r.Revenue).ThenBy(r => r.BrandName)
            .ToList();
    }

    private IQueryable<Domain.Entities.Order> ScopedOrders(DateTime? from, DateTime? to)
    {
        var orders = _db.Orders.AsNoTracking().Where(o => o.Status != OrderStatus.Cancelled);
        if (from is not null)
        {
            var fromUtc = AsUtcDate(from.Value);
            orders = orders.Where(o => o.CreatedAt >= fromUtc);
        }
        if (to is not null)
        {
            var toExclusive = AsUtcDate(to.Value).AddDays(1);
            orders = orders.Where(o => o.CreatedAt < toExclusive);
        }
        return orders;
    }

    private sealed record ReportLine(Guid OrderId, Guid? CategoryId, Guid? BrandId, int Quantity, decimal Line);

    private async Task<List<ReportLine>> ScopedLinesAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var orderIds = ScopedOrders(from, to).Select(o => o.Id);

        // Left-join order lines to products (order lines survive product deletion).
        var rows = await (
                from i in _db.OrderItems.AsNoTracking()
                where orderIds.Contains(i.OrderId)
                join p in _db.Products.AsNoTracking() on i.ProductId equals p.Id into gp
                from p in gp.DefaultIfEmpty()
                select new
                {
                    i.OrderId,
                    CategoryId = p != null ? (Guid?)p.CategoryId : null,
                    BrandId = p != null ? p.BrandId : null,
                    i.Quantity,
                    Line = i.UnitPrice * i.Quantity
                })
            .ToListAsync(ct);

        return rows.Select(r => new ReportLine(r.OrderId, r.CategoryId, r.BrandId, r.Quantity, r.Line)).ToList();
    }

    /// <summary>Postgres timestamptz rejects Unspecified kinds; treat incoming dates as UTC days.</summary>
    private static DateTime AsUtcDate(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
}
