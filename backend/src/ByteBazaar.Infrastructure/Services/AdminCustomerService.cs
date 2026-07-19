using ByteBazaar.Application.DTOs;
using ByteBazaar.Domain;
using ByteBazaar.Infrastructure.Identity;
using ByteBazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Infrastructure.Services;

/// <summary>
/// Read-only admin view over users in the Customer role with order aggregates.
/// Lives in Infrastructure because it queries the Identity tables directly.
/// </summary>
public class AdminCustomerService
{
    private readonly AppDbContext _db;

    public AdminCustomerService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResultDto<AdminCustomerListItemDto>> GetCustomersAsync(
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);

        var users = CustomersQuery();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            users = users.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                (u.Email != null && u.Email.ToLower().Contains(term)) ||
                (u.Phone != null && u.Phone.ToLower().Contains(term)));
        }

        var totalCount = await users.CountAsync(ct);
        var pageUsers = await users
            .OrderBy(u => u.FullName).ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new { u.Id, u.FullName, u.Email, u.Phone })
            .ToListAsync(ct);

        var ids = pageUsers.Select(u => u.Id).ToList();
        var stats = await OrderStatsAsync(ids, ct);

        var items = pageUsers.Select(u => new AdminCustomerListItemDto
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email ?? string.Empty,
            Phone = u.Phone,
            OrdersCount = stats.TryGetValue(u.Id, out var s) ? s.Count : 0,
            TotalSpent = stats.TryGetValue(u.Id, out var s2) ? s2.Spent : 0m
        }).ToList();

        return new PagedResultDto<AdminCustomerListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminCustomerDetailDto?> GetCustomerAsync(Guid id, CancellationToken ct = default)
    {
        var user = await CustomersQuery()
            .Where(u => u.Id == id)
            .Select(u => new { u.Id, u.FullName, u.Email, u.Phone })
            .FirstOrDefaultAsync(ct);
        if (user is null) return null;

        var stats = await OrderStatsAsync(new List<Guid> { id }, ct);

        var recentOrders = await _db.Orders.AsNoTracking()
            .Where(o => o.UserId == id)
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Take(5)
            .Select(o => new CustomerOrderSummaryDto
            {
                OrderNumber = o.OrderNumber,
                CreatedAt = o.CreatedAt,
                Status = o.Status,
                Total = o.Total
            })
            .ToListAsync(ct);

        return new AdminCustomerDetailDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Phone = user.Phone,
            OrdersCount = stats.TryGetValue(id, out var s) ? s.Count : 0,
            TotalSpent = stats.TryGetValue(id, out var s2) ? s2.Spent : 0m,
            RecentOrders = recentOrders
        };
    }

    private IQueryable<AppUser> CustomersQuery() =>
        from u in _db.Users.AsNoTracking()
        join ur in _db.UserRoles on u.Id equals ur.UserId
        join r in _db.Roles on ur.RoleId equals r.Id
        where r.Name == "Customer"
        select u;

    /// <summary>Order aggregates: count includes every order; spend excludes cancelled ones.</summary>
    private async Task<Dictionary<Guid, (int Count, decimal Spent)>> OrderStatsAsync(
        List<Guid> userIds, CancellationToken ct)
    {
        var rows = await _db.Orders.AsNoTracking()
            .Where(o => o.UserId != null && userIds.Contains(o.UserId.Value))
            .GroupBy(o => o.UserId!.Value)
            .Select(g => new
            {
                UserId = g.Key,
                Count = g.Count(),
                Spent = g.Sum(o => o.Status != OrderStatus.Cancelled ? o.Total : 0m)
            })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.UserId, r => (r.Count, r.Spent));
    }
}
