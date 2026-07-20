using System.Linq.Expressions;
using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using ByteBazaar.Application.Services;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using ByteBazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Tests;

/// <summary>
/// Concurrency hardening: two admins transitioning the same order must not both win.
/// The wrapper simulates a concurrent admin sneaking in between the service's read and
/// its atomic status-claim, which must surface as an OrderConflictException (409).
/// </summary>
public class TransitionConflictTests
{
    [Fact]
    public async Task Transition_WhenStatusChangedConcurrently_ThrowsConflictAndTouchesNothing()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var product = await db.Products.SingleAsync(p => p.Slug == "laptop-a"); // stock 5

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "BB-000001",
            Status = OrderStatus.Pending,
            PaymentMethod = PaymentMethod.COD,
            Subtotal = 100000m,
            ShippingFee = 250m,
            Total = 100250m,
            ShippingCode = "standard",
            FullName = "Test Customer",
            Phone = "0300-0000000",
            Email = "test@example.com",
            AddressLine = "Somewhere 1",
            City = "Lahore",
            Region = "Punjab"
        };
        order.Items.Add(new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id,
            ProductName = product.Name, ProductSlug = product.Slug, UnitPrice = 100000m, Quantity = 2
        });
        order.History.Add(new OrderStatusHistory
        {
            Id = Guid.NewGuid(), OrderId = order.Id, Status = OrderStatus.Pending, CreatedAt = DateTime.UtcNow
        });
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var notifications = new FakeNotificationQueue();
        var concurrentDb = new ConcurrentCancelDb(db);
        var service = new AdminOrderService(concurrentDb, notifications, new FakeRevalidator());

        // This request read the order as Pending and tries to confirm it, but "another admin"
        // cancels it right before the guard runs.
        await Assert.ThrowsAsync<OrderConflictException>(() =>
            service.TransitionStatusAsync(order.Id, new OrderStatusUpdateRequest { Status = OrderStatus.Confirmed }));

        // The concurrent cancellation stands; the losing request wrote nothing.
        Assert.Equal(OrderStatus.Cancelled, (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == order.Id)).Status);
        Assert.Equal(5, (await db.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id)).Stock); // no double decrement
        Assert.Empty(notifications.StatusChanges);
        Assert.Equal(1, await db.OrderStatusHistories.AsNoTracking().CountAsync(h => h.OrderId == order.Id));
    }

    [Fact]
    public async Task TryTransitionOrderStatus_VerifiesExpectedStatus()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var order = new Order
        {
            Id = Guid.NewGuid(), OrderNumber = "BB-000009", Status = OrderStatus.Pending,
            FullName = "X", Phone = "0", Email = "x@example.com", AddressLine = "A", City = "L", Region = "P"
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        Assert.True(await db.TryTransitionOrderStatusAsync(order.Id, OrderStatus.Pending, OrderStatus.Confirmed));
        Assert.False(await db.TryTransitionOrderStatusAsync(order.Id, OrderStatus.Confirmed, OrderStatus.Shipped));
        Assert.False(await db.TryTransitionOrderStatusAsync(Guid.NewGuid(), OrderStatus.Pending, OrderStatus.Confirmed));
    }

    /// <summary>
    /// Delegates everything to the real context, but a concurrent admin cancels the order
    /// just before the status guard executes (stale expected status).
    /// </summary>
    private sealed class ConcurrentCancelDb : IAppDbContext
    {
        private readonly AppDbContext _inner;

        public ConcurrentCancelDb(AppDbContext inner) => _inner = inner;

        public DbSet<Category> Categories => _inner.Categories;
        public DbSet<AttributeDefinition> AttributeDefinitions => _inner.AttributeDefinitions;
        public DbSet<Brand> Brands => _inner.Brands;
        public DbSet<Product> Products => _inner.Products;
        public DbSet<ProductImage> ProductImages => _inner.ProductImages;
        public DbSet<RefreshToken> RefreshTokens => _inner.RefreshTokens;
        public DbSet<Cart> Carts => _inner.Carts;
        public DbSet<CartItem> CartItems => _inner.CartItems;
        public DbSet<Order> Orders => _inner.Orders;
        public DbSet<OrderItem> OrderItems => _inner.OrderItems;
        public DbSet<OrderStatusHistory> OrderStatusHistories => _inner.OrderStatusHistories;
        public DbSet<Address> Addresses => _inner.Addresses;
        public DbSet<WishlistItem> WishlistItems => _inner.WishlistItems;
        public DbSet<Coupon> Coupons => _inner.Coupons;
        public DbSet<Banner> Banners => _inner.Banners;
        public DbSet<Redirect> Redirects => _inner.Redirects;

        public Expression<Func<Product, bool>> BuildAttributeFilter(string code, IReadOnlyList<string> values)
            => _inner.BuildAttributeFilter(code, values);

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
            => _inner.ExecuteInTransactionAsync(operation, cancellationToken);

        public Task<List<Guid>> DecrementStockAsync(IReadOnlyList<(Guid ProductId, int Quantity)> lines, CancellationToken cancellationToken = default)
            => _inner.DecrementStockAsync(lines, cancellationToken);

        public Task RestoreStockAsync(IReadOnlyList<(Guid ProductId, int Quantity)> lines, CancellationToken cancellationToken = default)
            => _inner.RestoreStockAsync(lines, cancellationToken);

        public async Task<bool> TryTransitionOrderStatusAsync(Guid orderId, OrderStatus expectedStatus, OrderStatus newStatus, CancellationToken cancellationToken = default)
        {
            // Simulate the concurrent admin winning the race.
            var order = await _inner.Orders.FirstAsync(o => o.Id == orderId, cancellationToken);
            if (order.Status == expectedStatus)
            {
                order.Status = OrderStatus.Cancelled;
                await _inner.SaveChangesAsync(cancellationToken);
            }
            return await _inner.TryTransitionOrderStatusAsync(orderId, expectedStatus, newStatus, cancellationToken);
        }

        public Task<bool> TryIncrementCouponUsageAsync(Guid couponId, CancellationToken cancellationToken = default)
            => _inner.TryIncrementCouponUsageAsync(couponId, cancellationToken);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _inner.SaveChangesAsync(cancellationToken);
    }
}
