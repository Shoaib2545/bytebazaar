using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using ByteBazaar.Infrastructure.Persistence;

namespace ByteBazaar.Tests.Integration;

/// <summary>Minimal, explicit seed data for the Postgres integration tests.</summary>
public static class IntegrationSeed
{
    public static readonly Guid LaptopsId = Guid.Parse("aaaaaaa1-0000-0000-0000-000000000001");
    public static readonly Guid AsusId = Guid.Parse("aaaaaaa1-0000-0000-0000-000000000002");

    public static async Task SeedCatalogAsync(AppDbContext db)
    {
        db.Categories.Add(new Category
        {
            Id = LaptopsId, Name = "Laptops", Slug = "laptops", SortOrder = 1, IsActive = true
        });
        db.Brands.Add(new Brand { Id = AsusId, Name = "Asus", Slug = "asus" });
        db.AttributeDefinitions.AddRange(
            new AttributeDefinition
            {
                Id = Guid.NewGuid(), CategoryId = LaptopsId, Name = "Processor", Code = "processor",
                Type = AttributeType.Select,
                Options = new List<string> { "Intel Core i5", "Intel Core i7", "AMD Ryzen 5" },
                IsFilterable = true, FilterWidget = FilterWidget.Checkbox, SortOrder = 1
            },
            new AttributeDefinition
            {
                Id = Guid.NewGuid(), CategoryId = LaptopsId, Name = "RAM", Code = "ram",
                Type = AttributeType.Select, Options = new List<string> { "8GB", "16GB", "32GB" },
                IsFilterable = true, FilterWidget = FilterWidget.Checkbox, SortOrder = 2
            });
        await db.SaveChangesAsync();
    }

    public static async Task<Product> AddProductAsync(
        AppDbContext db,
        string name,
        string slug,
        decimal price,
        int stock,
        Dictionary<string, string>? attributes = null)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = LaptopsId,
            BrandId = AsusId,
            Name = name,
            Slug = slug,
            Price = price,
            Stock = stock,
            Status = ProductStatus.Active,
            Attributes = attributes ?? new Dictionary<string, string>(),
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }
}
