using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using ByteBazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Tests;

public static class TestDbFactory
{
    public static readonly Guid LaptopsId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ComponentsId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid GpusId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid AsusId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid MsiId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"bytebazaar-tests-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    public static async Task<AppDbContext> CreateSeededAsync()
    {
        var db = Create();

        db.Categories.AddRange(
            new Category { Id = LaptopsId, Name = "Laptops", Slug = "laptops", SortOrder = 1, IsActive = true },
            new Category { Id = ComponentsId, Name = "Components", Slug = "components", SortOrder = 2, IsActive = true },
            new Category { Id = GpusId, ParentId = ComponentsId, Name = "Graphics Cards", Slug = "graphics-cards", SortOrder = 1, IsActive = true });

        db.AttributeDefinitions.AddRange(
            new AttributeDefinition
            {
                Id = Guid.NewGuid(), CategoryId = LaptopsId, Name = "Processor", Code = "processor",
                Type = AttributeType.Select, Options = new List<string> { "Intel Core i5", "Intel Core i7", "AMD Ryzen 5" },
                IsFilterable = true, FilterWidget = FilterWidget.Checkbox, SortOrder = 1
            },
            new AttributeDefinition
            {
                Id = Guid.NewGuid(), CategoryId = LaptopsId, Name = "RAM", Code = "ram",
                Type = AttributeType.Select, Options = new List<string> { "8GB", "16GB", "32GB" },
                IsFilterable = true, FilterWidget = FilterWidget.Checkbox, SortOrder = 2
            },
            new AttributeDefinition
            {
                Id = Guid.NewGuid(), CategoryId = GpusId, Name = "Memory", Code = "memory",
                Type = AttributeType.Select, Options = new List<string> { "8GB", "12GB" },
                IsFilterable = true, FilterWidget = FilterWidget.Checkbox, SortOrder = 1
            });

        db.Brands.AddRange(
            new Brand { Id = AsusId, Name = "Asus", Slug = "asus" },
            new Brand { Id = MsiId, Name = "MSI", Slug = "msi" });

        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Product Laptop(string name, string slug, Guid brandId, decimal price, decimal? salePrice,
            string processor, string ram, int daysOld) => new()
        {
            Id = Guid.NewGuid(),
            CategoryId = LaptopsId,
            BrandId = brandId,
            Name = name,
            Slug = slug,
            Price = price,
            SalePrice = salePrice,
            Stock = 5,
            Status = ProductStatus.Active,
            Attributes = new Dictionary<string, string> { ["processor"] = processor, ["ram"] = ram },
            CreatedAt = baseDate.AddDays(daysOld)
        };

        db.Products.AddRange(
            Laptop("Laptop A", "laptop-a", AsusId, 100000m, null, "Intel Core i5", "8GB", 1),
            Laptop("Laptop B", "laptop-b", AsusId, 200000m, 180000m, "Intel Core i7", "16GB", 2),
            Laptop("Laptop C", "laptop-c", MsiId, 300000m, null, "Intel Core i7", "32GB", 3),
            Laptop("Laptop D", "laptop-d", MsiId, 150000m, null, "AMD Ryzen 5", "16GB", 4),
            Laptop("Laptop E", "laptop-e", AsusId, 250000m, null, "AMD Ryzen 5", "8GB", 5),
            new Product
            {
                Id = Guid.NewGuid(), CategoryId = LaptopsId, BrandId = AsusId,
                Name = "Draft Laptop", Slug = "draft-laptop", Price = 999999m, Stock = 1,
                Status = ProductStatus.Draft,
                Attributes = new Dictionary<string, string> { ["processor"] = "Intel Core i7", ["ram"] = "16GB" },
                CreatedAt = baseDate.AddDays(6)
            },
            new Product
            {
                Id = Guid.NewGuid(), CategoryId = GpusId, BrandId = MsiId,
                Name = "GPU A", Slug = "gpu-a", Price = 120000m, Stock = 3,
                Status = ProductStatus.Active,
                Attributes = new Dictionary<string, string> { ["memory"] = "12GB" },
                CreatedAt = baseDate.AddDays(7)
            });

        await db.SaveChangesAsync();
        return db;
    }
}
