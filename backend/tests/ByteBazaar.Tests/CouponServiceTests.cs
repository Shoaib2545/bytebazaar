using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using ByteBazaar.Application.Services;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using ByteBazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Tests;

public class CouponServiceTests
{
    private static readonly Guid AnonId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private static (CartService Cart, CouponService Coupons) CreateServices(AppDbContext db)
    {
        var cart = new CartService(db);
        return (cart, new CouponService(db, cart));
    }

    private static Coupon MakeCoupon(
        string code, CouponType type = CouponType.Percent, decimal value = 10m,
        decimal? minOrderAmount = null, int? maxUses = null, int usedCount = 0,
        DateTime? validFrom = null, DateTime? validTo = null, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Type = type,
        Value = value,
        MinOrderAmount = minOrderAmount,
        MaxUses = maxUses,
        UsedCount = usedCount,
        ValidFrom = validFrom,
        ValidTo = validTo,
        IsActive = isActive
    };

    private static async Task AddLaptopAAsync(CartService cart, AppDbContext db, int quantity = 1)
    {
        var product = await db.Products.SingleAsync(p => p.Slug == "laptop-a"); // 100000, no sale
        await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = product.Id, Quantity = quantity });
    }

    // ----- Validation matrix -----

    [Fact]
    public async Task Apply_UnknownCode_Rejected()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (cart, coupons) = CreateServices(db);
        await AddLaptopAAsync(cart, db);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => coupons.ApplyAsync(null, AnonId, "NOPE"));
        Assert.Contains("Unknown coupon", ex.Message);
    }

    [Fact]
    public async Task Apply_InactiveCoupon_Rejected()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.Add(MakeCoupon("OFF10", isActive: false));
        await db.SaveChangesAsync();
        var (cart, coupons) = CreateServices(db);
        await AddLaptopAAsync(cart, db);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => coupons.ApplyAsync(null, AnonId, "OFF10"));
        Assert.Contains("not active", ex.Message);
    }

    [Fact]
    public async Task Apply_NotYetValidCoupon_Rejected()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.Add(MakeCoupon("SOON", validFrom: DateTime.UtcNow.AddDays(1)));
        await db.SaveChangesAsync();
        var (cart, coupons) = CreateServices(db);
        await AddLaptopAAsync(cart, db);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => coupons.ApplyAsync(null, AnonId, "SOON"));
        Assert.Contains("not valid yet", ex.Message);
    }

    [Fact]
    public async Task Apply_ExpiredCoupon_Rejected()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.Add(MakeCoupon("OLD", validTo: DateTime.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();
        var (cart, coupons) = CreateServices(db);
        await AddLaptopAAsync(cart, db);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => coupons.ApplyAsync(null, AnonId, "OLD"));
        Assert.Contains("expired", ex.Message);
    }

    [Fact]
    public async Task Apply_MinOrderNotMet_Rejected()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.Add(MakeCoupon("BIG", minOrderAmount: 150000m));
        await db.SaveChangesAsync();
        var (cart, coupons) = CreateServices(db);
        await AddLaptopAAsync(cart, db); // subtotal 100000 < 150000

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => coupons.ApplyAsync(null, AnonId, "BIG"));
        Assert.Contains("minimum order", ex.Message);
    }

    [Fact]
    public async Task Apply_MaxedOutCoupon_Rejected()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.Add(MakeCoupon("FULL", maxUses: 3, usedCount: 3));
        await db.SaveChangesAsync();
        var (cart, coupons) = CreateServices(db);
        await AddLaptopAAsync(cart, db);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => coupons.ApplyAsync(null, AnonId, "FULL"));
        Assert.Contains("usage limit", ex.Message);
    }

    [Fact]
    public async Task Apply_EmptyCart_Rejected()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.Add(MakeCoupon("OFF10"));
        await db.SaveChangesAsync();
        var (_, coupons) = CreateServices(db);

        await Assert.ThrowsAsync<BadRequestException>(() => coupons.ApplyAsync(null, AnonId, "OFF10"));
    }

    // ----- Discount math -----

    [Fact]
    public async Task Apply_PercentCoupon_ComputesDiscountAndTotal()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.Add(MakeCoupon("OFF10", CouponType.Percent, 10m));
        await db.SaveChangesAsync();
        var (cart, coupons) = CreateServices(db);
        await AddLaptopAAsync(cart, db, quantity: 2); // subtotal 200000

        var dto = await coupons.ApplyAsync(null, AnonId, "off10"); // case-insensitive input

        Assert.Equal("OFF10", dto.CouponCode);
        Assert.Equal(20000m, dto.Discount);
        Assert.Equal(200000m, dto.Subtotal);
        Assert.Equal(180000m, dto.Total);
    }

    [Fact]
    public void PercentDiscount_RoundsToTwoDecimals()
    {
        var coupon = MakeCoupon("OFF10", CouponType.Percent, 10m);
        // 10% of 5555.55 = 555.555 -> 555.56 (away from zero)
        Assert.Equal(555.56m, CouponRules.ComputeDiscount(coupon, 5555.55m));
    }

    [Fact]
    public async Task Apply_FixedCoupon_ComputesDiscount()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.Add(MakeCoupon("FLAT500", CouponType.Fixed, 500m));
        await db.SaveChangesAsync();
        var (cart, coupons) = CreateServices(db);
        await AddLaptopAAsync(cart, db);

        var dto = await coupons.ApplyAsync(null, AnonId, "FLAT500");

        Assert.Equal(500m, dto.Discount);
        Assert.Equal(99500m, dto.Total);
    }

    [Fact]
    public void FixedDiscount_IsCappedAtSubtotal()
    {
        var coupon = MakeCoupon("FLAT500", CouponType.Fixed, 500m);
        Assert.Equal(300m, CouponRules.ComputeDiscount(coupon, 300m));
    }

    // ----- Apply / remove / recompute -----

    [Fact]
    public async Task Apply_ReplacesPreviousCoupon()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.AddRange(MakeCoupon("OFF10", value: 10m), MakeCoupon("OFF20", value: 20m));
        await db.SaveChangesAsync();
        var (cart, coupons) = CreateServices(db);
        await AddLaptopAAsync(cart, db);

        await coupons.ApplyAsync(null, AnonId, "OFF10");
        var dto = await coupons.ApplyAsync(null, AnonId, "OFF20");

        Assert.Equal("OFF20", dto.CouponCode);
        Assert.Equal(20000m, dto.Discount);
    }

    [Fact]
    public async Task Remove_ClearsCouponFromCart()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.Add(MakeCoupon("OFF10"));
        await db.SaveChangesAsync();
        var (cart, coupons) = CreateServices(db);
        await AddLaptopAAsync(cart, db);
        await coupons.ApplyAsync(null, AnonId, "OFF10");

        var dto = await coupons.RemoveAsync(null, AnonId);

        Assert.Null(dto.CouponCode);
        Assert.Equal(0m, dto.Discount);
        Assert.Equal(dto.Subtotal, dto.Total);
        Assert.Null((await db.Carts.SingleAsync()).CouponCode);
    }

    [Fact]
    public async Task CartRead_SilentlyDropsCouponThatBecameInvalid()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var coupon = MakeCoupon("OFF10");
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();
        var (cart, coupons) = CreateServices(db);
        await AddLaptopAAsync(cart, db);
        await coupons.ApplyAsync(null, AnonId, "OFF10");

        coupon.IsActive = false;
        await db.SaveChangesAsync();

        var dto = await cart.GetCartAsync(null, AnonId);

        Assert.Null(dto.CouponCode);
        Assert.Equal(0m, dto.Discount);
        Assert.Equal(dto.Subtotal, dto.Total);
    }

    [Fact]
    public async Task CartRead_DropsCouponWhenSubtotalFallsBelowMinOrder()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.Add(MakeCoupon("BIG", minOrderAmount: 150000m));
        await db.SaveChangesAsync();
        var (cart, coupons) = CreateServices(db);
        var product = await db.Products.SingleAsync(p => p.Slug == "laptop-a");
        await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = product.Id, Quantity = 2 }); // 200000
        var applied = await coupons.ApplyAsync(null, AnonId, "BIG");
        Assert.Equal("BIG", applied.CouponCode);

        var dto = await cart.UpdateItemAsync(null, AnonId, product.Id, 1); // 100000 < 150000

        Assert.Null(dto.CouponCode);
        Assert.Equal(0m, dto.Discount);
    }

    [Fact]
    public async Task CouponDiscount_AppliesOnEffectiveSalePrices()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        db.Coupons.Add(MakeCoupon("OFF10"));
        await db.SaveChangesAsync();
        var (cart, coupons) = CreateServices(db);
        var onSale = await db.Products.SingleAsync(p => p.Slug == "laptop-b"); // 200000, sale 180000 (unbounded)
        await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = onSale.Id, Quantity = 1 });

        var dto = await coupons.ApplyAsync(null, AnonId, "OFF10");

        Assert.Equal(180000m, dto.Subtotal);
        Assert.Equal(18000m, dto.Discount);
        Assert.Equal(162000m, dto.Total);
    }
}
