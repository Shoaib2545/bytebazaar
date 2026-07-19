using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using ByteBazaar.Application.Services;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using ByteBazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Tests;

public class CheckoutCouponTests
{
    private static readonly Guid AnonId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

    private static (CheckoutService Checkout, CartService Cart, CouponService Coupons) CreateServices(AppDbContext db)
    {
        var cart = new CartService(db);
        var checkout = new CheckoutService(db, cart, new DefaultShippingOptionsProvider(), new FakeNotificationQueue());
        return (checkout, cart, new CouponService(db, cart));
    }

    private static CheckoutRequest ValidRequest() => new()
    {
        FullName = "Ali Khan",
        Phone = "0301-1234567",
        Email = "ali@example.com",
        AddressLine = "House 1, Street 2",
        City = "Karachi",
        Region = "Sindh",
        ShippingCode = "standard",
        PaymentMethod = PaymentMethod.COD
    };

    private static Coupon Percent10(int? maxUses = null, int usedCount = 0) => new()
    {
        Id = Guid.NewGuid(),
        Code = "SAVE10",
        Type = CouponType.Percent,
        Value = 10m,
        MaxUses = maxUses,
        UsedCount = usedCount,
        IsActive = true
    };

    [Fact]
    public async Task Checkout_WithPercentCoupon_AppliesDiscountAndIncrementsUsedCount()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.Add(Percent10(maxUses: 5));
        await db.SaveChangesAsync();
        var (checkout, cart, coupons) = CreateServices(db);
        var a = await db.Products.SingleAsync(p => p.Slug == "laptop-a"); // 100000

        await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = a.Id, Quantity = 2 });
        await coupons.ApplyAsync(null, AnonId, "SAVE10");

        var result = await checkout.CheckoutAsync(null, AnonId, ValidRequest());

        // subtotal 200000 - discount 20000 + shipping 250
        Assert.Equal("SAVE10", result.CouponCode);
        Assert.Equal(20000m, result.Discount);
        Assert.Equal(180250m, result.Total);

        var order = await db.Orders.SingleAsync();
        Assert.Equal(200000m, order.Subtotal);
        Assert.Equal("SAVE10", order.CouponCode);
        Assert.Equal(20000m, order.Discount);
        Assert.Equal(180250m, order.Total);

        Assert.Equal(1, (await db.Coupons.SingleAsync()).UsedCount);

        // The cart's coupon is consumed along with the items.
        Assert.Null((await db.Carts.SingleAsync()).CouponCode);
        var after = await cart.GetCartAsync(null, AnonId);
        Assert.Null(after.CouponCode);
    }

    [Fact]
    public async Task Checkout_WithFixedCoupon_AppliesFlatDiscount()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.Add(new Coupon
        {
            Id = Guid.NewGuid(), Code = "FLAT500", Type = CouponType.Fixed, Value = 500m,
            MinOrderAmount = 50000m, IsActive = true
        });
        await db.SaveChangesAsync();
        var (checkout, cart, coupons) = CreateServices(db);
        var a = await db.Products.SingleAsync(p => p.Slug == "laptop-a");

        await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = a.Id, Quantity = 1 });
        await coupons.ApplyAsync(null, AnonId, "FLAT500");

        var result = await checkout.CheckoutAsync(null, AnonId, ValidRequest());

        Assert.Equal(500m, result.Discount);
        Assert.Equal(100000m - 500m + 250m, result.Total);
    }

    [Fact]
    public async Task Checkout_ExhaustedCoupon_RejectedAndNoOrderCreated()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.Add(Percent10(maxUses: 1, usedCount: 1));
        await db.SaveChangesAsync();
        var (checkout, cart, _) = CreateServices(db);
        var a = await db.Products.SingleAsync(p => p.Slug == "laptop-a");
        await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = a.Id, Quantity = 1 });

        // Bypass apply-time validation: the last use was claimed after the coupon was applied.
        var cartEntity = await db.Carts.SingleAsync();
        cartEntity.CouponCode = "SAVE10";
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => checkout.CheckoutAsync(null, AnonId, ValidRequest()));
        Assert.Contains("usage limit", ex.Message);
        Assert.False(await db.Orders.AnyAsync());
        Assert.Equal(1, (await db.Coupons.SingleAsync()).UsedCount);
        Assert.True(await db.CartItems.AnyAsync()); // cart untouched
    }

    [Fact]
    public async Task Checkout_LastRemainingUse_SucceedsAndExhaustsCoupon()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.Add(Percent10(maxUses: 3, usedCount: 2));
        await db.SaveChangesAsync();
        var (checkout, cart, coupons) = CreateServices(db);
        var a = await db.Products.SingleAsync(p => p.Slug == "laptop-a");
        await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = a.Id, Quantity = 1 });
        await coupons.ApplyAsync(null, AnonId, "SAVE10");

        var result = await checkout.CheckoutAsync(null, AnonId, ValidRequest());

        Assert.Equal(10000m, result.Discount);
        Assert.Equal(3, (await db.Coupons.SingleAsync()).UsedCount);
    }

    [Fact]
    public async Task Checkout_CouponExpiredAfterApply_Rejected()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var coupon = Percent10();
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();
        var (checkout, cart, coupons) = CreateServices(db);
        var a = await db.Products.SingleAsync(p => p.Slug == "laptop-a");
        await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = a.Id, Quantity = 1 });
        await coupons.ApplyAsync(null, AnonId, "SAVE10");

        coupon.ValidTo = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => checkout.CheckoutAsync(null, AnonId, ValidRequest()));
        Assert.Contains("expired", ex.Message);
        Assert.False(await db.Orders.AnyAsync());
        Assert.Equal(0, coupon.UsedCount);
    }

    [Fact]
    public async Task Checkout_WithoutCoupon_HasZeroDiscount()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (checkout, cart, _) = CreateServices(db);
        var a = await db.Products.SingleAsync(p => p.Slug == "laptop-a");
        await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = a.Id, Quantity = 1 });

        var result = await checkout.CheckoutAsync(null, AnonId, ValidRequest());

        Assert.Null(result.CouponCode);
        Assert.Equal(0m, result.Discount);
        Assert.Equal(100250m, result.Total);
    }

    [Fact]
    public async Task TryIncrementCouponUsage_GuardsMaxUses()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var unlimited = Percent10();
        var limited = new Coupon { Id = Guid.NewGuid(), Code = "LIMITED", Type = CouponType.Fixed, Value = 100m, MaxUses = 1, UsedCount = 1, IsActive = true };
        db.Coupons.AddRange(unlimited, limited);
        await db.SaveChangesAsync();

        Assert.True(await db.TryIncrementCouponUsageAsync(unlimited.Id));
        Assert.Equal(1, unlimited.UsedCount);

        Assert.False(await db.TryIncrementCouponUsageAsync(limited.Id));
        Assert.Equal(1, limited.UsedCount);

        Assert.False(await db.TryIncrementCouponUsageAsync(Guid.NewGuid()));
    }
}
