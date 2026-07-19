using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ByteBazaar.Tests;

public class AdminCatalogServiceTests
{
    // Regression: replacement images carry explicit Guid keys, so navigation fixup
    // used to track them as Modified (UPDATE against nonexistent rows) instead of Added.
    [Fact]
    public async Task UpdateProduct_ReplacesExistingImages_WithoutTrackingConflict()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var product = await db.Products.Include(p => p.Images).FirstAsync(p => p.Slug == "laptop-a");
        db.ProductImages.AddRange(
            new ProductImage { Id = Guid.NewGuid(), ProductId = product.Id, Url = "https://img/old-1.png", SortOrder = 1 },
            new ProductImage { Id = Guid.NewGuid(), ProductId = product.Id, Url = "https://img/old-2.png", SortOrder = 2 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new AdminCatalogService(db, new FakeRevalidator());
        var request = new ProductUpsertRequest
        {
            Name = product.Name,
            Slug = product.Slug,
            CategoryId = product.CategoryId,
            BrandId = product.BrandId,
            Price = product.Price,
            Stock = product.Stock,
            Status = ProductStatus.Active,
            Images = new List<string> { "https://img/new-1.png", "https://img/new-2.png", "https://img/new-3.png" },
            Attributes = new Dictionary<string, string>(product.Attributes),
        };

        var dto = await service.UpdateProductAsync(product.Id, request);

        Assert.NotNull(dto);
        var urls = await db.ProductImages.AsNoTracking()
            .Where(i => i.ProductId == product.Id)
            .OrderBy(i => i.SortOrder)
            .Select(i => i.Url)
            .ToListAsync();
        Assert.Equal(new[] { "https://img/new-1.png", "https://img/new-2.png", "https://img/new-3.png" }, urls);
    }
}
