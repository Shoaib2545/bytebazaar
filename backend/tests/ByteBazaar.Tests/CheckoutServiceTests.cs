using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using ByteBazaar.Application.Services;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using ByteBazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Tests;

public class CheckoutServiceTests
{
    private static readonly Guid AnonId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static (CheckoutService Checkout, CartService Cart, FakeNotificationQueue Notifications) CreateServices(AppDbContext db)
    {
        var cart = new CartService(db);
        var notifications = new FakeNotificationQueue();
        var checkout = new CheckoutService(db, cart, new DefaultShippingOptionsProvider(), notifications);
        return (checkout, cart, notifications);
    }

    private static CheckoutRequest ValidRequest(string shippingCode = "standard") => new()
    {
        FullName = "Ali Khan",
        Phone = "0301-1234567",
        Email = "ali@example.com",
        AddressLine = "House 1, Street 2",
        City = "Karachi",
        Region = "Sindh",
        ShippingCode = shippingCode,
        PaymentMethod = PaymentMethod.COD,
        Notes = "Ring the bell."
    };

    [Fact]
    public async Task Checkout_HappyPath_CreatesPendingOrderWithSnapshotsAndClearsCart()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (checkout, cart, notifications) = CreateServices(db);
        var a = await db.Products.SingleAsync(p => p.Slug == "laptop-a"); // 100000
        var b = await db.Products.SingleAsync(p => p.Slug == "laptop-b"); // 200000, sale 180000

        await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = a.Id, Quantity = 2 });
        await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = b.Id, Quantity = 1 });

        var result = await checkout.CheckoutAsync(null, AnonId, ValidRequest());

        // Totals: effective prices (sale price wins) + shipping fee.
        Assert.Equal(2 * 100000m + 180000m + 250m, result.Total);
        Assert.Equal(OrderStatus.Pending, result.Status);
        Assert.Equal("BB-000001", result.OrderNumber);

        var order = await db.Orders.Include(o => o.Items).Include(o => o.History).SingleAsync();
        Assert.Equal(380000m, order.Subtotal);
        Assert.Equal(250m, order.ShippingFee);
        Assert.Equal(380250m, order.Total);
        Assert.Equal(PaymentMethod.COD, order.PaymentMethod);
        Assert.Equal("Ali Khan", order.FullName);

        // Price/name snapshots.
        var lineB = order.Items.Single(i => i.ProductId == b.Id);
        Assert.Equal(180000m, lineB.UnitPrice);
        Assert.Equal("Laptop B", lineB.ProductName);
        Assert.Equal("laptop-b", lineB.ProductSlug);

        // History row written.
        var history = Assert.Single(order.History);
        Assert.Equal(OrderStatus.Pending, history.Status);

        // Cart cleared.
        var after = await cart.GetCartAsync(null, AnonId);
        Assert.Empty(after.Items);

        // Stock is NOT decremented at checkout (that happens on Confirmed).
        Assert.Equal(5, (await db.Products.SingleAsync(p => p.Id == a.Id)).Stock);

        // Notification enqueued.
        var placed = Assert.Single(notifications.Placed);
        Assert.Equal("BB-000001", placed.OrderNumber);
        Assert.Equal("ali@example.com", placed.Email);
    }

    [Fact]
    public async Task Checkout_EmptyCart_Rejected()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (checkout, _, _) = CreateServices(db);

        await Assert.ThrowsAsync<BadRequestException>(() => checkout.CheckoutAsync(null, AnonId, ValidRequest()));
    }

    [Fact]
    public async Task Checkout_UnknownShippingCode_Rejected()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (checkout, cart, _) = CreateServices(db);
        var a = await db.Products.SingleAsync(p => p.Slug == "laptop-a");
        await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = a.Id, Quantity = 1 });

        await Assert.ThrowsAsync<BadRequestException>(() => checkout.CheckoutAsync(null, AnonId, ValidRequest("overnight")));
    }

    [Fact]
    public async Task Checkout_InsufficientStock_Rejected_AndNothingPersisted()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (checkout, _, _) = CreateServices(db);
        var a = await db.Products.SingleAsync(p => p.Slug == "laptop-a"); // stock 5

        // Bypass CartService validation: stock dropped after the item went into the cart.
        var cartEntity = new Cart { Id = Guid.NewGuid(), AnonymousId = AnonId, UpdatedAt = DateTime.UtcNow };
        cartEntity.Items.Add(new CartItem { Id = Guid.NewGuid(), CartId = cartEntity.Id, ProductId = a.Id, Quantity = 10 });
        db.Carts.Add(cartEntity);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<BadRequestException>(() => checkout.CheckoutAsync(null, AnonId, ValidRequest()));
        Assert.False(await db.Orders.AnyAsync());
        Assert.True(await db.CartItems.AnyAsync()); // cart untouched
    }

    [Fact]
    public async Task Checkout_ExpressShipping_UsesExpressFee()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (checkout, cart, _) = CreateServices(db);
        var a = await db.Products.SingleAsync(p => p.Slug == "laptop-a");
        await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = a.Id, Quantity = 1 });

        var result = await checkout.CheckoutAsync(null, AnonId, ValidRequest("express"));
        Assert.Equal(100000m + 600m, result.Total);
    }

    [Fact]
    public async Task Checkout_GeneratesSequentialUniqueOrderNumbers()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (checkout, cart, _) = CreateServices(db);
        var a = await db.Products.SingleAsync(p => p.Slug == "laptop-a");

        await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = a.Id, Quantity = 1 });
        var first = await checkout.CheckoutAsync(null, AnonId, ValidRequest());

        await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = a.Id, Quantity = 1 });
        var second = await checkout.CheckoutAsync(null, AnonId, ValidRequest());

        Assert.Equal("BB-000001", first.OrderNumber);
        Assert.Equal("BB-000002", second.OrderNumber);
        Assert.NotEqual(first.OrderNumber, second.OrderNumber);
        Assert.Equal(2, await db.Orders.Select(o => o.OrderNumber).Distinct().CountAsync());
    }

    [Fact]
    public void ShippingOptions_MatchContract()
    {
        var options = new DefaultShippingOptionsProvider().GetOptions();
        Assert.Equal(2, options.Count);
        Assert.Equal(250m, options.Single(o => o.Code == "standard").Fee);
        Assert.Equal(600m, options.Single(o => o.Code == "express").Fee);
    }
}
