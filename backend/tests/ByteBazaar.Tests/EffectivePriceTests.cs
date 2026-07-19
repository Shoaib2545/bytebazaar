using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using ByteBazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Tests;

/// <summary>
/// The effective-price rule: SalePrice applies ONLY while now is within [SaleStart, SaleEnd]
/// (null bound = unbounded). Covers catalog listing/filter/sort/detail, cart and checkout.
/// </summary>
public class EffectivePriceTests
{
    private static readonly Guid AnonId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid DealsId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private static async Task<AppDbContext> CreateDbAsync()
    {
        var db = TestDbFactory.Create();
        var now = DateTime.UtcNow;

        db.Categories.Add(new Category { Id = DealsId, Name = "Deals", Slug = "deals", SortOrder = 1, IsActive = true });

        Product Deal(string name, string slug, DateTime? saleStart, DateTime? saleEnd, int daysOld) => new()
        {
            Id = Guid.NewGuid(),
            CategoryId = DealsId,
            Name = name,
            Slug = slug,
            Price = 100000m,
            SalePrice = 80000m,
            SaleStart = saleStart,
            SaleEnd = saleEnd,
            Stock = 10,
            Status = ProductStatus.Active,
            CreatedAt = now.AddDays(-daysOld)
        };

        db.Products.AddRange(
            Deal("Deal Active", "deal-active", now.AddDays(-1), now.AddDays(1), 3),
            Deal("Deal Future", "deal-future", now.AddDays(5), now.AddDays(10), 2),
            Deal("Deal Expired", "deal-expired", now.AddDays(-10), now.AddDays(-5), 1));

        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Listing_ExposesSalePriceOnlyWithinWindow()
    {
        await using var db = await CreateDbAsync();
        var service = new CatalogService(db);

        var result = await service.GetCategoryProductsAsync("deals", new CatalogQuery());

        Assert.NotNull(result);
        Assert.Equal(80000m, result!.Items.Single(i => i.Slug == "deal-active").SalePrice);
        Assert.Null(result.Items.Single(i => i.Slug == "deal-future").SalePrice);
        Assert.Null(result.Items.Single(i => i.Slug == "deal-expired").SalePrice);
    }

    [Fact]
    public async Task NullBounds_MeanUnboundedWindow()
    {
        // The seeded laptop-b has SalePrice with null start/end -> always active.
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CatalogService(db);

        var result = await service.SearchAsync("laptop b", new CatalogQuery());

        Assert.Equal(180000m, result.Items.Single().SalePrice);
    }

    [Fact]
    public async Task PriceFilter_UsesWindowedEffectivePrice()
    {
        await using var db = await CreateDbAsync();
        var service = new CatalogService(db);

        // Cap 90000: only the active sale (80000) qualifies; future/expired are at 100000.
        var below = await service.GetCategoryProductsAsync("deals", new CatalogQuery { PriceMax = 90000m });
        Assert.Equal("deal-active", Assert.Single(below!.Items).Slug);

        // Floor 90000: future + expired qualify at their base price.
        var above = await service.GetCategoryProductsAsync("deals", new CatalogQuery { PriceMin = 90000m });
        Assert.Equal(2, above!.TotalCount);
        Assert.All(above.Items, i => Assert.Contains(i.Slug, new[] { "deal-future", "deal-expired" }));
    }

    [Fact]
    public async Task PriceSort_UsesWindowedEffectivePrice()
    {
        await using var db = await CreateDbAsync();
        var service = new CatalogService(db);

        var asc = await service.GetCategoryProductsAsync("deals", new CatalogQuery { Sort = "price_asc" });

        // Active sale (80000) first; the other two tie at 100000.
        Assert.Equal("deal-active", asc!.Items.First().Slug);

        var desc = await service.GetCategoryProductsAsync("deals", new CatalogQuery { Sort = "price_desc" });
        Assert.Equal("deal-active", desc!.Items.Last().Slug);
    }

    [Fact]
    public async Task FiltersPriceRange_UsesWindowedEffectivePrice()
    {
        await using var db = await CreateDbAsync();
        var service = new CatalogService(db);

        var filters = await service.GetCategoryFiltersAsync("deals");

        Assert.Equal(80000m, filters!.PriceRange.Min);
        Assert.Equal(100000m, filters.PriceRange.Max);
    }

    [Fact]
    public async Task ProductDetail_HidesSalePriceOutsideWindow()
    {
        await using var db = await CreateDbAsync();
        var service = new CatalogService(db);

        Assert.Equal(80000m, (await service.GetProductAsync("deal-active"))!.SalePrice);
        Assert.Null((await service.GetProductAsync("deal-future"))!.SalePrice);
        Assert.Null((await service.GetProductAsync("deal-expired"))!.SalePrice);
    }

    [Fact]
    public async Task Featured_ReturnsFeaturedActiveProductsNewestFirst_WithWindowedSalePrice()
    {
        await using var db = await CreateDbAsync();
        foreach (var product in db.Products.Where(p => p.Slug != "deal-future"))
            product.IsFeatured = true;
        db.Products.Add(new Product
        {
            Id = Guid.NewGuid(), CategoryId = DealsId, Name = "Draft Featured", Slug = "draft-featured",
            Price = 1m, IsFeatured = true, Status = ProductStatus.Draft, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new CatalogService(db);

        var featured = await service.GetFeaturedAsync(8);

        // Draft excluded, newest first (deal-expired is newest of the featured pair).
        Assert.Equal(new[] { "deal-expired", "deal-active" }, featured.Select(f => f.Slug).ToArray());
        Assert.Equal(80000m, featured.Single(f => f.Slug == "deal-active").SalePrice);
        Assert.Null(featured.Single(f => f.Slug == "deal-expired").SalePrice);

        var limited = await service.GetFeaturedAsync(1);
        Assert.Single(limited);
    }

    [Fact]
    public async Task Cart_ChargesBasePriceOutsideSaleWindow()
    {
        await using var db = await CreateDbAsync();
        var cart = new CartService(db);
        var expired = await db.Products.SingleAsync(p => p.Slug == "deal-expired");
        var active = await db.Products.SingleAsync(p => p.Slug == "deal-active");

        var dto = await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = expired.Id, Quantity = 1 });
        Assert.Equal(100000m, dto.Items.Single().UnitPrice);

        dto = await cart.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = active.Id, Quantity = 1 });
        Assert.Equal(80000m, dto.Items.Single(i => i.ProductId == active.Id).UnitPrice);
        Assert.Equal(180000m, dto.Subtotal);
    }

    [Fact]
    public async Task Checkout_SnapshotsWindowedEffectivePrices()
    {
        await using var db = await CreateDbAsync();
        var cartService = new CartService(db);
        var checkout = new CheckoutService(db, cartService, new DefaultShippingOptionsProvider(), new FakeNotificationQueue());
        var expired = await db.Products.SingleAsync(p => p.Slug == "deal-expired");
        var future = await db.Products.SingleAsync(p => p.Slug == "deal-future");
        var active = await db.Products.SingleAsync(p => p.Slug == "deal-active");

        await cartService.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = expired.Id, Quantity = 1 });
        await cartService.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = future.Id, Quantity = 1 });
        await cartService.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = active.Id, Quantity = 1 });

        var result = await checkout.CheckoutAsync(null, AnonId, new CheckoutRequest
        {
            FullName = "Ali Khan", Phone = "0301-1234567", Email = "ali@example.com",
            AddressLine = "House 1", City = "Karachi", Region = "Sindh",
            ShippingCode = "standard", PaymentMethod = PaymentMethod.COD
        });

        // 100000 (expired) + 100000 (future) + 80000 (active) + 250 shipping.
        Assert.Equal(280250m, result.Total);

        var order = await db.Orders.Include(o => o.Items).SingleAsync();
        Assert.Equal(100000m, order.Items.Single(i => i.ProductId == expired.Id).UnitPrice);
        Assert.Equal(100000m, order.Items.Single(i => i.ProductId == future.Id).UnitPrice);
        Assert.Equal(80000m, order.Items.Single(i => i.ProductId == active.Id).UnitPrice);
    }
}
