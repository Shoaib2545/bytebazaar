using ByteBazaar.Application.Exceptions;
using ByteBazaar.Application.Services;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using ByteBazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Tests;

public class ReportServiceTests
{
    private static int _orderSequence;

    private static async Task<Order> SeedOrderAsync(
        AppDbContext db, OrderStatus status, DateTime createdAt, params (string Slug, int Quantity)[] lines)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"BB-{Interlocked.Increment(ref _orderSequence):D6}",
            Status = status,
            PaymentMethod = PaymentMethod.COD,
            ShippingFee = 250m,
            ShippingCode = "standard",
            FullName = "Report Customer",
            Phone = "0300-0000000",
            Email = "report@example.com",
            AddressLine = "Somewhere 1",
            City = "Lahore",
            Region = "Punjab",
            CreatedAt = createdAt
        };

        foreach (var (slug, quantity) in lines)
        {
            var product = await db.Products.SingleAsync(p => p.Slug == slug);
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
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    [Fact]
    public async Task SalesReport_GroupsByDay_ExcludesCancelled_AndRespectsRange()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new ReportService(db);
        var day1 = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        var day2 = new DateTime(2026, 7, 3, 15, 30, 0, DateTimeKind.Utc);

        var o1 = await SeedOrderAsync(db, OrderStatus.Delivered, day1, ("laptop-a", 1));           // 100250
        var o2 = await SeedOrderAsync(db, OrderStatus.Pending, day1.AddHours(2), ("laptop-b", 1)); // 180250
        await SeedOrderAsync(db, OrderStatus.Cancelled, day1.AddHours(3), ("laptop-c", 1));        // excluded
        var o4 = await SeedOrderAsync(db, OrderStatus.Confirmed, day2, ("laptop-d", 2));           // 300250

        var rows = await service.GetSalesAsync(null, null, "day");

        Assert.Equal(2, rows.Count);
        var first = rows.Single(r => r.Period == "2026-07-01");
        Assert.Equal(2, first.Orders);
        Assert.Equal(o1.Total + o2.Total, first.Revenue);
        var second = rows.Single(r => r.Period == "2026-07-03");
        Assert.Equal(1, second.Orders);
        Assert.Equal(o4.Total, second.Revenue);

        // Range: only day1 (to is inclusive through end of day).
        var ranged = await service.GetSalesAsync(new DateTime(2026, 7, 1), new DateTime(2026, 7, 1), "day");
        Assert.Single(ranged);
        Assert.Equal("2026-07-01", ranged[0].Period);

        // Empty range is fine.
        var empty = await service.GetSalesAsync(new DateTime(2027, 1, 1), new DateTime(2027, 1, 31), "day");
        Assert.Empty(empty);
    }

    [Fact]
    public async Task SalesReport_UnknownGroupBy_Rejected()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new ReportService(db);

        await Assert.ThrowsAsync<BadRequestException>(() => service.GetSalesAsync(null, null, "hour"));
    }

    [Fact]
    public async Task ByCategory_AggregatesUnitsOrdersAndRevenue()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new ReportService(db);
        var when = new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc);

        // laptop-a (100000) x2 + gpu-a (120000) x1 in one order; laptop-b (sale 180000) x1 in another.
        await SeedOrderAsync(db, OrderStatus.Delivered, when, ("laptop-a", 2), ("gpu-a", 1));
        await SeedOrderAsync(db, OrderStatus.Confirmed, when, ("laptop-b", 1));
        await SeedOrderAsync(db, OrderStatus.Cancelled, when, ("laptop-c", 5)); // excluded

        var rows = await service.GetByCategoryAsync(null, null);

        var laptops = rows.Single(r => r.CategoryName == "Laptops");
        Assert.Equal(2, laptops.Orders);
        Assert.Equal(3, laptops.Units);
        Assert.Equal(2 * 100000m + 180000m, laptops.Revenue);

        var gpus = rows.Single(r => r.CategoryName == "Graphics Cards");
        Assert.Equal(1, gpus.Orders);
        Assert.Equal(1, gpus.Units);
        Assert.Equal(120000m, gpus.Revenue);
    }

    [Fact]
    public async Task ByBrand_AggregatesUnitsOrdersAndRevenue()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new ReportService(db);
        var when = new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc);

        // Asus: laptop-a x1 + laptop-b x1; MSI: laptop-c x1 + gpu-a x2.
        await SeedOrderAsync(db, OrderStatus.Delivered, when, ("laptop-a", 1), ("laptop-c", 1));
        await SeedOrderAsync(db, OrderStatus.Pending, when, ("laptop-b", 1), ("gpu-a", 2));

        var rows = await service.GetByBrandAsync(null, null);

        var asus = rows.Single(r => r.BrandName == "Asus");
        Assert.Equal(2, asus.Orders);
        Assert.Equal(2, asus.Units);
        Assert.Equal(100000m + 180000m, asus.Revenue);

        var msi = rows.Single(r => r.BrandName == "MSI");
        Assert.Equal(2, msi.Orders);
        Assert.Equal(3, msi.Units);
        Assert.Equal(300000m + 2 * 120000m, msi.Revenue);
    }

    [Fact]
    public async Task ByCategory_DeletedProduct_FallsBackToPlaceholder()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new ReportService(db);
        var when = new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc);

        var order = await SeedOrderAsync(db, OrderStatus.Delivered, when, ("laptop-a", 1));
        // Rewire the snapshot line to a product that no longer exists.
        order.Items[0].ProductId = Guid.NewGuid();
        await db.SaveChangesAsync();

        var rows = await service.GetByCategoryAsync(null, null);

        var row = Assert.Single(rows);
        Assert.Equal("(deleted)", row.CategoryName);
        Assert.Equal(100000m, row.Revenue);
    }

    [Fact]
    public async Task Reports_NoOrders_ReturnEmptyLists()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new ReportService(db);

        Assert.Empty(await service.GetSalesAsync(null, null, null));
        Assert.Empty(await service.GetByCategoryAsync(null, null));
        Assert.Empty(await service.GetByBrandAsync(null, null));
    }

    // ----- CSV writer -----

    [Fact]
    public void Csv_EscapesCommasQuotesAndNewlines()
    {
        var csv = CsvWriter.Write(
            new[] { "categoryName", "orders", "revenue" },
            new List<IReadOnlyList<object?>>
            {
                new object?[] { "Laptops, Gaming", 2, 1234.50m },
                new object?[] { "The \"Best\" GPUs", 1, 99m },
                new object?[] { "Multi\nLine", 0, null }
            });

        var lines = csv.Split("\r\n");
        Assert.Equal("categoryName,orders,revenue", lines[0]);
        Assert.Equal("\"Laptops, Gaming\",2,1234.50", lines[1]);
        Assert.Equal("\"The \"\"Best\"\" GPUs\",1,99", lines[2]);
        // The embedded newline stays inside one quoted field.
        Assert.StartsWith("\"Multi", lines[3]);
        Assert.Contains("Line\",0,", csv);
    }

    [Fact]
    public void Csv_PlainValues_AreNotQuoted()
    {
        Assert.Equal("plain", CsvWriter.Escape("plain"));
        Assert.Equal("100250.5", CsvWriter.Escape(100250.5m));
        Assert.Equal(string.Empty, CsvWriter.Escape(null));
    }
}
