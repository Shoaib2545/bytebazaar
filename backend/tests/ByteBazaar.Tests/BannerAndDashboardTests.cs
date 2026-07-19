using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using ByteBazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Tests;

public class BannerAndDashboardTests
{
    private static Banner MakeBanner(
        string title, BannerPlacement placement = BannerPlacement.Hero, int sortOrder = 0,
        bool isActive = true, DateTime? startsAt = null, DateTime? endsAt = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        ImageUrl = $"https://example.test/{Guid.NewGuid():N}.png",
        Placement = placement,
        SortOrder = sortOrder,
        IsActive = isActive,
        StartsAt = startsAt,
        EndsAt = endsAt
    };

    [Fact]
    public async Task PublicBanners_OnlyActiveWithinWindow()
    {
        await using var db = TestDbFactory.Create();
        var now = DateTime.UtcNow;
        db.Banners.AddRange(
            MakeBanner("Current", sortOrder: 2),
            MakeBanner("Windowed", sortOrder: 1, startsAt: now.AddDays(-1), endsAt: now.AddDays(1)),
            MakeBanner("Inactive", isActive: false),
            MakeBanner("Future", startsAt: now.AddDays(1)),
            MakeBanner("Expired", endsAt: now.AddDays(-1)),
            MakeBanner("Strip Banner", BannerPlacement.Strip));
        await db.SaveChangesAsync();
        var service = new BannerService(db);

        var banners = await service.GetActiveBannersAsync();

        Assert.Equal(3, banners.Count);
        // Hero placement first, ordered by sortOrder.
        Assert.Equal(new[] { "Windowed", "Current", "Strip Banner" }, banners.Select(b => b.Title).ToArray());
        Assert.Equal(BannerPlacement.Strip, banners.Last().Placement);
    }

    [Fact]
    public async Task AdminBanners_CrudRoundtrip()
    {
        await using var db = TestDbFactory.Create();
        var service = new BannerService(db);

        var created = await service.CreateAsync(new BannerUpsertRequest
        {
            Title = "PC Week Sale",
            Subtitle = "Big savings",
            ImageUrl = "https://example.test/hero.png",
            LinkUrl = "/category/laptops",
            Placement = BannerPlacement.Hero,
            SortOrder = 1,
            IsActive = true
        });
        Assert.Equal("PC Week Sale", created.Title);

        var updated = await service.UpdateAsync(created.Id, new BannerUpsertRequest
        {
            Title = "GPU Week Sale",
            ImageUrl = created.ImageUrl,
            Placement = BannerPlacement.Strip,
            SortOrder = 5,
            IsActive = false
        });
        Assert.Equal("GPU Week Sale", updated!.Title);
        Assert.Equal(BannerPlacement.Strip, updated.Placement);
        Assert.False(updated.IsActive);

        Assert.Single(await service.GetBannersAsync());
        Assert.Empty(await service.GetActiveBannersAsync());

        Assert.True(await service.DeleteAsync(created.Id));
        Assert.False(await service.DeleteAsync(created.Id));
        Assert.Empty(await service.GetBannersAsync());
    }

    [Fact]
    public async Task Dashboard_TopProductsAndSalesLast7Days()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var laptopA = await db.Products.SingleAsync(p => p.Slug == "laptop-a"); // 100000
        var laptopC = await db.Products.SingleAsync(p => p.Slug == "laptop-c"); // 300000
        var today = DateTime.UtcNow.Date;

        Order MakeOrder(string number, OrderStatus status, DateTime createdAt, params (Product P, int Qty)[] lines)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(), OrderNumber = number, Status = status,
                ShippingFee = 250m, ShippingCode = "standard",
                FullName = "D", Phone = "0", Email = "d@example.com",
                AddressLine = "A", City = "L", Region = "P", CreatedAt = createdAt
            };
            foreach (var (p, qty) in lines)
            {
                order.Items.Add(new OrderItem
                {
                    Id = Guid.NewGuid(), OrderId = order.Id, ProductId = p.Id,
                    ProductName = p.Name, ProductSlug = p.Slug, UnitPrice = p.Price, Quantity = qty
                });
            }
            order.Subtotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);
            order.Total = order.Subtotal + order.ShippingFee;
            return order;
        }

        var o1 = MakeOrder("BB-000101", OrderStatus.Delivered, today.AddDays(-2).AddHours(9), (laptopA, 3));
        var o2 = MakeOrder("BB-000102", OrderStatus.Pending, today.AddHours(1), (laptopC, 1), (laptopA, 1));
        var cancelled = MakeOrder("BB-000103", OrderStatus.Cancelled, today.AddHours(2), (laptopC, 5));
        db.Orders.AddRange(o1, o2, cancelled);
        await db.SaveChangesAsync();

        var summary = await new DashboardService(db).GetSummaryAsync();

        // Top products: laptop-a 4 units / 400000; laptop-c 1 unit / 300000 (cancelled excluded).
        Assert.Equal(2, summary.TopProducts.Count);
        Assert.Equal(laptopA.Id, summary.TopProducts[0].ProductId);
        Assert.Equal(4, summary.TopProducts[0].Units);
        Assert.Equal(400000m, summary.TopProducts[0].Revenue);
        Assert.Equal(laptopC.Id, summary.TopProducts[1].ProductId);
        Assert.Equal(300000m, summary.TopProducts[1].Revenue);

        // 7 zero-filled entries ending today.
        Assert.Equal(7, summary.SalesLast7Days.Count);
        Assert.Equal(today.ToString("yyyy-MM-dd"), summary.SalesLast7Days[^1].Date);
        Assert.Equal(o2.Total, summary.SalesLast7Days[^1].Revenue); // cancelled excluded
        Assert.Equal(o1.Total, summary.SalesLast7Days[^3].Revenue);
        Assert.Equal(0m, summary.SalesLast7Days[0].Revenue);
    }
}
