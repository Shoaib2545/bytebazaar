using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using Xunit;

namespace ByteBazaar.Tests;

public class CatalogServiceTests
{
    [Fact]
    public async Task FilterBySingleAttribute_ReturnsMatchingProducts()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CatalogService(db);

        var query = new CatalogQuery();
        query.Attributes["processor"] = new List<string> { "Intel Core i7" };

        var result = await service.GetCategoryProductsAsync("laptops", query);

        Assert.NotNull(result);
        Assert.Equal(2, result!.TotalCount);
        Assert.All(result.Items, i => Assert.Contains(i.Slug, new[] { "laptop-b", "laptop-c" }));
    }

    [Fact]
    public async Task FilterByTwoAttributes_AppliesAndSemantics()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CatalogService(db);

        var query = new CatalogQuery();
        query.Attributes["processor"] = new List<string> { "Intel Core i7" };
        query.Attributes["ram"] = new List<string> { "16GB" };

        var result = await service.GetCategoryProductsAsync("laptops", query);

        Assert.NotNull(result);
        var item = Assert.Single(result!.Items);
        Assert.Equal("laptop-b", item.Slug);
    }

    [Fact]
    public async Task FilterByMultipleValuesWithinAttribute_AppliesOrSemantics()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CatalogService(db);

        var query = new CatalogQuery();
        query.Attributes["processor"] = new List<string> { "Intel Core i5", "AMD Ryzen 5" };

        var result = await service.GetCategoryProductsAsync("laptops", query);

        Assert.NotNull(result);
        Assert.Equal(3, result!.TotalCount);
        Assert.All(result.Items, i => Assert.Contains(i.Slug, new[] { "laptop-a", "laptop-d", "laptop-e" }));
    }

    [Fact]
    public async Task FilterByPriceRange_UsesEffectivePrice()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CatalogService(db);

        // Laptop B costs 200000 but is on sale for 180000, so a 180000 cap must include it.
        var query = new CatalogQuery { PriceMin = 120000m, PriceMax = 180000m };

        var result = await service.GetCategoryProductsAsync("laptops", query);

        Assert.NotNull(result);
        Assert.Equal(2, result!.TotalCount);
        Assert.All(result.Items, i => Assert.Contains(i.Slug, new[] { "laptop-b", "laptop-d" }));
    }

    [Fact]
    public async Task FilterByBrand_ReturnsOnlyThatBrand()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CatalogService(db);

        var query = new CatalogQuery { Brands = new List<string> { "msi" } };

        var result = await service.GetCategoryProductsAsync("laptops", query);

        Assert.NotNull(result);
        Assert.Equal(2, result!.TotalCount);
        Assert.All(result.Items, i => Assert.Contains(i.Slug, new[] { "laptop-c", "laptop-d" }));
    }

    [Fact]
    public async Task PaginationAndPriceSort_Work()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CatalogService(db);

        var page1 = await service.GetCategoryProductsAsync("laptops",
            new CatalogQuery { Page = 1, PageSize = 2, Sort = "price_asc" });
        var page2 = await service.GetCategoryProductsAsync("laptops",
            new CatalogQuery { Page = 2, PageSize = 2, Sort = "price_asc" });

        Assert.NotNull(page1);
        Assert.NotNull(page2);
        Assert.Equal(5, page1!.TotalCount);
        Assert.Equal(2, page1.Items.Count);

        // Effective prices: A=100000, D=150000, B=180000 (sale), E=250000, C=300000
        Assert.Equal(new[] { "laptop-a", "laptop-d" }, page1.Items.Select(i => i.Slug).ToArray());
        Assert.Equal(new[] { "laptop-b", "laptop-e" }, page2!.Items.Select(i => i.Slug).ToArray());

        var desc = await service.GetCategoryProductsAsync("laptops",
            new CatalogQuery { Page = 1, PageSize = 1, Sort = "price_desc" });
        Assert.Equal("laptop-c", desc!.Items.Single().Slug);
    }

    [Fact]
    public async Task NewestSort_IsDefault()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CatalogService(db);

        var result = await service.GetCategoryProductsAsync("laptops", new CatalogQuery { PageSize = 1 });

        Assert.Equal("laptop-e", result!.Items.Single().Slug);
    }

    [Fact]
    public async Task DraftProducts_AreExcluded()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CatalogService(db);

        var result = await service.GetCategoryProductsAsync("laptops", new CatalogQuery());

        Assert.Equal(5, result!.TotalCount);
        Assert.DoesNotContain(result.Items, i => i.Slug == "draft-laptop");
    }

    [Fact]
    public async Task ParentCategory_IncludesDescendantProducts()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CatalogService(db);

        var result = await service.GetCategoryProductsAsync("components", new CatalogQuery());

        Assert.NotNull(result);
        var item = Assert.Single(result!.Items);
        Assert.Equal("gpu-a", item.Slug);
    }

    [Fact]
    public async Task Filters_ReturnOptionCountsBrandsAndPriceRange()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CatalogService(db);

        var filters = await service.GetCategoryFiltersAsync("laptops");

        Assert.NotNull(filters);
        var processor = filters!.Attributes.Single(a => a.Code == "processor");
        Assert.Equal("Select", processor.Type);
        Assert.Equal("Checkbox", processor.Widget);
        Assert.Equal(1, processor.Options.Single(o => o.Value == "Intel Core i5").Count);
        Assert.Equal(2, processor.Options.Single(o => o.Value == "Intel Core i7").Count);
        Assert.Equal(2, processor.Options.Single(o => o.Value == "AMD Ryzen 5").Count);

        var asus = filters.Brands.Single(b => b.Slug == "asus");
        Assert.Equal(3, asus.Count);

        Assert.Equal(100000m, filters.PriceRange.Min);
        Assert.Equal(300000m, filters.PriceRange.Max);
    }

    [Fact]
    public async Task UnknownCategorySlug_ReturnsNull()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CatalogService(db);

        Assert.Null(await service.GetCategoryProductsAsync("nope", new CatalogQuery()));
        Assert.Null(await service.GetCategoryFiltersAsync("nope"));
    }

    [Fact]
    public async Task Search_MatchesByNameCaseInsensitive()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CatalogService(db);

        var result = await service.SearchAsync("laptop b", new CatalogQuery());

        var item = Assert.Single(result.Items);
        Assert.Equal("laptop-b", item.Slug);
    }
}
