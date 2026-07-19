using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using ByteBazaar.Application.Services;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using ByteBazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Tests;

public class AdminOrderServiceTests
{
    private static (AdminOrderService Service, FakeNotificationQueue Notifications, FakeRevalidator Revalidator) CreateService(AppDbContext db)
    {
        var notifications = new FakeNotificationQueue();
        var revalidator = new FakeRevalidator();
        return (new AdminOrderService(db, notifications, revalidator), notifications, revalidator);
    }

    private static async Task<Order> SeedOrderAsync(
        AppDbContext db, OrderStatus status, params (string Slug, int Quantity)[] lines)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"BB-{await db.Orders.CountAsync() + 1:D6}",
            Status = status,
            PaymentMethod = PaymentMethod.COD,
            ShippingFee = 250m,
            ShippingCode = "standard",
            FullName = "Test Customer",
            Phone = "0300-0000000",
            Email = "test@example.com",
            AddressLine = "Somewhere 1",
            City = "Lahore",
            Region = "Punjab",
            CreatedAt = DateTime.UtcNow
        };

        foreach (var (slug, quantity) in lines)
        {
            var product = await db.Products.Include(p => p.Images).SingleAsync(p => p.Slug == slug);
            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                ProductSlug = product.Slug,
                UnitPrice = product.SalePrice ?? product.Price,
                Quantity = quantity
            });
        }

        order.Subtotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);
        order.Total = order.Subtotal + order.ShippingFee;
        order.History.Add(new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    [Theory]
    // Valid transitions
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered, true)]
    // Invalid transitions
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Pending, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Confirmed, false)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Confirmed, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Delivered, false)]
    public async Task TransitionMatrix_IsEnforced(OrderStatus from, OrderStatus to, bool valid)
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (service, _, _) = CreateService(db);
        var order = await SeedOrderAsync(db, from, ("laptop-a", 1));

        var request = new OrderStatusUpdateRequest { Status = to, Note = "test" };

        if (valid)
        {
            var result = await service.TransitionStatusAsync(order.Id, request);
            Assert.NotNull(result);
            Assert.Equal(to, result!.Status);
        }
        else
        {
            await Assert.ThrowsAsync<BadRequestException>(() => service.TransitionStatusAsync(order.Id, request));
            Assert.Equal(from, (await db.Orders.SingleAsync(o => o.Id == order.Id)).Status);
        }
    }

    [Fact]
    public async Task Confirm_DecrementsStock_WritesHistory_AndNotifies()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (service, notifications, revalidator) = CreateService(db);
        var order = await SeedOrderAsync(db, OrderStatus.Pending, ("laptop-a", 2), ("gpu-a", 1));

        var result = await service.TransitionStatusAsync(order.Id, new OrderStatusUpdateRequest { Status = OrderStatus.Confirmed, Note = "ok" });

        Assert.Equal(OrderStatus.Confirmed, result!.Status);
        Assert.Equal(3, (await db.Products.SingleAsync(p => p.Slug == "laptop-a")).Stock); // 5 - 2
        Assert.Equal(2, (await db.Products.SingleAsync(p => p.Slug == "gpu-a")).Stock);    // 3 - 1

        var history = await db.OrderStatusHistories.Where(h => h.OrderId == order.Id).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.Status == OrderStatus.Confirmed && h.Note == "ok");

        var change = Assert.Single(notifications.StatusChanges);
        Assert.Equal("Confirmed", change.Status);

        // Stock changed => storefront product pages revalidated.
        Assert.Contains("/product/laptop-a", revalidator.Paths);
        Assert.Contains("/product/gpu-a", revalidator.Paths);
    }

    [Fact]
    public async Task Confirm_InsufficientStock_RejectsWholeTransition()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (service, notifications, _) = CreateService(db);
        // laptop-a has stock 5 (enough), gpu-a has stock 3 (not enough for 5).
        var order = await SeedOrderAsync(db, OrderStatus.Pending, ("laptop-a", 2), ("gpu-a", 5));

        await Assert.ThrowsAsync<StockConflictException>(() =>
            service.TransitionStatusAsync(order.Id, new OrderStatusUpdateRequest { Status = OrderStatus.Confirmed }));

        // Whole transition rejected: no stock was touched anywhere and status is unchanged.
        Assert.Equal(5, (await db.Products.SingleAsync(p => p.Slug == "laptop-a")).Stock);
        Assert.Equal(3, (await db.Products.SingleAsync(p => p.Slug == "gpu-a")).Stock);
        Assert.Equal(OrderStatus.Pending, (await db.Orders.SingleAsync(o => o.Id == order.Id)).Status);
        Assert.Empty(notifications.StatusChanges);
        Assert.Equal(1, await db.OrderStatusHistories.CountAsync(h => h.OrderId == order.Id));
    }

    [Fact]
    public async Task CancelAfterConfirm_RestoresStock()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (service, _, _) = CreateService(db);
        var order = await SeedOrderAsync(db, OrderStatus.Pending, ("laptop-a", 2));

        await service.TransitionStatusAsync(order.Id, new OrderStatusUpdateRequest { Status = OrderStatus.Confirmed });
        Assert.Equal(3, (await db.Products.SingleAsync(p => p.Slug == "laptop-a")).Stock);

        var result = await service.TransitionStatusAsync(order.Id, new OrderStatusUpdateRequest { Status = OrderStatus.Cancelled });

        Assert.Equal(OrderStatus.Cancelled, result!.Status);
        Assert.Equal(5, (await db.Products.SingleAsync(p => p.Slug == "laptop-a")).Stock);
    }

    [Fact]
    public async Task CancelPendingOrder_DoesNotTouchStock()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (service, _, _) = CreateService(db);
        var order = await SeedOrderAsync(db, OrderStatus.Pending, ("laptop-a", 2));

        await service.TransitionStatusAsync(order.Id, new OrderStatusUpdateRequest { Status = OrderStatus.Cancelled });

        Assert.Equal(5, (await db.Products.SingleAsync(p => p.Slug == "laptop-a")).Stock);
    }

    [Fact]
    public async Task Transition_UnknownOrder_ReturnsNull()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (service, _, _) = CreateService(db);

        var result = await service.TransitionStatusAsync(
            Guid.NewGuid(), new OrderStatusUpdateRequest { Status = OrderStatus.Confirmed });
        Assert.Null(result);
    }

    [Fact]
    public async Task GetOrders_FiltersByStatusAndSearch()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (service, _, _) = CreateService(db);
        await SeedOrderAsync(db, OrderStatus.Pending, ("laptop-a", 1));
        await SeedOrderAsync(db, OrderStatus.Shipped, ("laptop-b", 1));

        var pending = await service.GetOrdersAsync("Pending", null, 1, 20);
        Assert.Single(pending.Items);
        Assert.Equal(OrderStatus.Pending, pending.Items[0].Status);

        var byNumber = await service.GetOrdersAsync(null, "bb-000002", 1, 20);
        Assert.Single(byNumber.Items);

        await Assert.ThrowsAsync<BadRequestException>(() => service.GetOrdersAsync("NotAStatus", null, 1, 20));
    }
}
