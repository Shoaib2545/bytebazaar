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
            LowStock = lowStock
        };
    }
}
