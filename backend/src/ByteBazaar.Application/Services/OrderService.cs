using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

public class OrderService
{
    private readonly IAppDbContext _db;

    public OrderService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResultDto<OrderListItemDto>> GetOrdersAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 10 : pageSize, 1, 100);

        var orders = _db.Orders.AsNoTracking().Where(o => o.UserId == userId);
        var totalCount = await orders.CountAsync(ct);
        var items = await orders
            .OrderByDescending(o => o.CreatedAt).ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderListItemDto
            {
                OrderNumber = o.OrderNumber,
                CreatedAt = o.CreatedAt,
                Status = o.Status,
                Total = o.Total,
                ItemCount = o.Items.Sum(i => i.Quantity)
            })
            .ToListAsync(ct);

        return new PagedResultDto<OrderListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<OrderDetailDto?> GetOrderAsync(Guid userId, string orderNumber, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.History)
            .FirstOrDefaultAsync(o => o.UserId == userId && o.OrderNumber == orderNumber, ct);
        return order is null ? null : ToDetail(order, new OrderDetailDto());
    }

    internal static T ToDetail<T>(Order order, T dto) where T : OrderDetailDto
    {
        dto.OrderNumber = order.OrderNumber;
        dto.CreatedAt = order.CreatedAt;
        dto.Status = order.Status;
        dto.PaymentMethod = order.PaymentMethod;
        dto.Subtotal = order.Subtotal;
        dto.ShippingFee = order.ShippingFee;
        dto.Total = order.Total;
        dto.ShippingAddress = new ShippingAddressDto
        {
            FullName = order.FullName,
            Phone = order.Phone,
            Email = order.Email,
            AddressLine = order.AddressLine,
            City = order.City,
            Region = order.Region
        };
        dto.Items = order.Items
            .OrderBy(i => i.ProductName)
            .Select(i => new OrderItemDto
            {
                ProductId = i.ProductId,
                Name = i.ProductName,
                Slug = i.ProductSlug,
                ImageUrl = i.ImageUrl,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                LineTotal = i.UnitPrice * i.Quantity
            })
            .ToList();
        dto.History = order.History
            .OrderBy(h => h.CreatedAt)
            .Select(h => new OrderHistoryDto { Status = h.Status, Note = h.Note, CreatedAt = h.CreatedAt })
            .ToList();
        return dto;
    }
}
