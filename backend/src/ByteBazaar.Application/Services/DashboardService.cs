using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Domain;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

public class DashboardService
{
    private const int LowStockThreshold = 5;

    private readonly IAppDbContext _db;

    public DashboardService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var todayUtc = DateTime.UtcNow.Date;

        var ordersToday = await _db.Orders.AsNoTracking()
            .CountAsync(o => o.CreatedAt >= todayUtc, ct);

        var salesToday = await _db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= todayUtc && o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0m;

        var pendingOrders = await _db.Orders.AsNoTracking()
            .CountAsync(o => o.Status == OrderStatus.Pending, ct);

        var totalProducts = await _db.Products.AsNoTracking().CountAsync(ct);

        var lowStock = await _db.Products.AsNoTracking()
            .Where(p => p.Stock <= LowStockThreshold)
            .OrderBy(p => p.Stock).ThenBy(p => p.Name)
            .Take(20)
            .Select(p => new LowStockProductDto { Id = p.Id, Name = p.Name, Stock = p.Stock })
            .ToListAsync(ct);

        return new DashboardSummaryDto
        {
            OrdersToday = ordersToday,
            SalesToday = salesToday,
            PendingOrders = pendingOrders,
            TotalProducts = totalProducts,
            LowStock = lowStock,
            TopProducts = await GetTopProductsAsync(ct),
            SalesLast7Days = await GetSalesLast7DaysAsync(todayUtc, ct)
        };
    }

    /// <summary>Top 5 products by revenue across all non-cancelled orders.</summary>
    private async Task<List<TopProductDto>> GetTopProductsAsync(CancellationToken ct)
    {
        var lines = await _db.OrderItems.AsNoTracking()
            .Where(i => i.Order!.Status != OrderStatus.Cancelled)
            .Select(i => new { i.ProductId, i.ProductName, i.Quantity, Line = i.UnitPrice * i.Quantity })
            .ToListAsync(ct);

        return lines
            .GroupBy(l => l.ProductId)
            .Select(g => new TopProductDto
            {
                ProductId = g.Key,
                Name = g.OrderByDescending(l => l.Line).First().ProductName,
                Units = g.Sum(l => l.Quantity),
                Revenue = g.Sum(l => l.Line)
            })
            .OrderByDescending(t => t.Revenue).ThenBy(t => t.Name)
            .Take(5)
            .ToList();
    }

    /// <summary>Revenue per day for the last 7 days (today inclusive), zero-filled.</summary>
    private async Task<List<DailySalesDto>> GetSalesLast7DaysAsync(DateTime todayUtc, CancellationToken ct)
    {
        var windowStart = todayUtc.AddDays(-6);
        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= windowStart && o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.CreatedAt, o.Total })
            .ToListAsync(ct);

        var byDay = orders
            .GroupBy(o => o.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => g.Sum(o => o.Total));

        return Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = windowStart.AddDays(offset);
                return new DailySalesDto
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    Revenue = byDay.TryGetValue(day, out var revenue) ? revenue : 0m
                };
            })
            .ToList();
    }
}
