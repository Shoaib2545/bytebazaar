using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

public class AdminOrderService
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> ValidTransitions = new()
    {
        [OrderStatus.Pending] = new[] { OrderStatus.Confirmed, OrderStatus.Cancelled },
        [OrderStatus.Confirmed] = new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
        [OrderStatus.Shipped] = new[] { OrderStatus.Delivered },
        [OrderStatus.Delivered] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>()
    };

    private readonly IAppDbContext _db;
    private readonly IOrderNotificationQueue _notifications;
    private readonly IStorefrontRevalidator _revalidator;

    public AdminOrderService(IAppDbContext db, IOrderNotificationQueue notifications, IStorefrontRevalidator revalidator)
    {
        _db = db;
        _notifications = notifications;
        _revalidator = revalidator;
    }

    public async Task<PagedResultDto<AdminOrderListItemDto>> GetOrdersAsync(
        string? status, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);

        var orders = _db.Orders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
                throw new BadRequestException($"Unknown order status \"{status}\".");
            orders = orders.Where(o => o.Status == parsed);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            orders = orders.Where(o =>
                o.OrderNumber.ToLower().Contains(term) ||
                o.FullName.ToLower().Contains(term) ||
                o.Phone.ToLower().Contains(term) ||
                o.Email.ToLower().Contains(term));
        }

        var totalCount = await orders.CountAsync(ct);
        var items = await orders
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new AdminOrderListItemDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                CreatedAt = o.CreatedAt,
                CustomerName = o.FullName,
                Phone = o.Phone,
                City = o.City,
                Status = o.Status,
                Total = o.Total,
                ItemCount = o.Items.Sum(i => i.Quantity)
            })
            .ToListAsync(ct);

        return new PagedResultDto<AdminOrderListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminOrderDetailDto?> GetOrderAsync(Guid id, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.History)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        return order is null ? null : ToDto(order);
    }

    public async Task<AdminOrderDetailDto?> TransitionStatusAsync(
        Guid id, OrderStatusUpdateRequest request, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .Include(o => o.History)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return null;

        var from = order.Status;
        var to = request.Status;

        if (!ValidTransitions.TryGetValue(from, out var allowed) || !allowed.Contains(to))
            throw new BadRequestException($"Cannot transition an order from {from} to {to}.");

        var lines = order.Items.Select(i => (i.ProductId, i.Quantity)).ToList();
        var touchesStock = to == OrderStatus.Confirmed || (to == OrderStatus.Cancelled && from == OrderStatus.Confirmed);

        await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            // Concurrency guard: atomically claim the transition (WHERE Status = expected).
            // If another admin already transitioned this order, 0 rows match -> 409, and no
            // stock is touched. Stock changes below run in the same transaction, so a stock
            // failure rolls the claimed status back too.
            if (!await _db.TryTransitionOrderStatusAsync(order.Id, from, to, innerCt))
                throw new OrderConflictException(
                    $"Order {order.OrderNumber} is no longer in status {from} — another user has updated it. Reload the order and try again.");

            if (to == OrderStatus.Confirmed)
            {
                var failed = await _db.DecrementStockAsync(lines, innerCt);
                if (failed.Count > 0)
                {
                    var names = order.Items
                        .Where(i => failed.Contains(i.ProductId))
                        .Select(i => i.ProductName);
                    throw new StockConflictException(
                        $"Insufficient stock to confirm the order: {string.Join(", ", names)}.");
                }
            }
            else if (to == OrderStatus.Cancelled && from == OrderStatus.Confirmed)
            {
                await _db.RestoreStockAsync(lines, innerCt);
            }

            order.Status = to;
            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Status = to,
                Note = request.Note,
                CreatedAt = DateTime.UtcNow
            }); // fixup appends it to order.History
            await _db.SaveChangesAsync(innerCt);
        }, ct);

        await _notifications.EnqueueStatusChangedAsync(order.OrderNumber, to.ToString(), order.Email);

        if (touchesStock)
        {
            var paths = order.Items.Select(i => $"/product/{i.ProductSlug}").Distinct().ToArray();
            _revalidator.Revalidate(paths);
        }

        return ToDto(order);
    }

    private static AdminOrderDetailDto ToDto(Order order)
    {
        var dto = OrderService.ToDetail(order, new AdminOrderDetailDto());
        dto.Id = order.Id;
        dto.CustomerEmail = order.Email;
        dto.CustomerPhone = order.Phone;
        dto.Notes = order.Notes;
        return dto;
    }
}
