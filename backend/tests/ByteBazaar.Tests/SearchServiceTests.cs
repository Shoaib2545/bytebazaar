using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ByteBazaar.Tests;

public class SearchServiceTests
{
    private static async Task<(SearchService Service, FakeSearchIndex Index, Infrastructure.Persistence.AppDbContext Db)>
        CreateAsync(bool indexPopulated = true, bool engineUp = true)
    {
        var db = await TestDbFactory.CreateSeededAsync();
        var index = new FakeSearchIndex { Available = true };

        if (indexPopulated)
        {
            var indexer = new SearchIndexingService(db, index);
            await indexer.ReindexAllAsync();
        }

        index.Available = engineUp;
        return (new SearchService(db, index, new CatalogService(db)), index, db);
    }

    [Fact]
    public async Task Suggest_UsesSearchEngine_WhenAvailable()
    {
        var (service, _, db) = await CreateAsync();
        await using var _db = db;

        var result = await service.SuggestAsync("Laptop", 3);

        Assert.Equal(SearchSource.SearchEngine, result.Source);
        Assert.Equal(3, result.Products.Count);
        // Draft products are never indexed, so they cannot surface in suggestions.
        Assert.DoesNotContain(result.Products, p => p.Slug == "draft-laptop");
        Assert.Equal(5, result.TotalProducts);
    }

    [Fact]
    public async Task Suggest_FallsBackToDatabase_WhenEngineIsDown()
    {
        var (service, index, db) = await CreateAsync(engineUp: false);
        await using var _db = db;

        var result = await service.SuggestAsync("Laptop", 3);

        Assert.False(index.Available);
        Assert.Equal(SearchSource.Database, result.Source);
        Assert.Equal(3, result.Products.Count);
        Assert.DoesNotContain(result.Products, p => p.Slug == "draft-laptop");
    }

    [Fact]
    public async Task Suggest_ReturnsCategoryAndBrandShortcuts_EvenWhenEngineIsDown()
    {
        var (service, _, db) = await CreateAsync(engineUp: false);
        await using var _db = db;

        var result = await service.SuggestAsync("as", 5);

        Assert.Contains(result.Brands, b => b.Slug == "asus");
        var graphics = await service.SuggestAsync("graphics", 5);
        Assert.Contains(graphics.Categories, c => c.Slug == "graphics-cards");
    }

    [Fact]
    public async Task Suggest_WithBlankQuery_ReturnsEmptyPayload()
    {
        var (service, _, db) = await CreateAsync();
        await using var _db = db;

        var result = await service.SuggestAsync("   ", 5);

        Assert.Empty(result.Products);
        Assert.Empty(result.Categories);
        Assert.Empty(result.Brands);
        Assert.Equal(0, result.TotalProducts);
    }

    [Fact]
    public async Task Suggest_ClampsLimit()
    {
        var (service, _, db) = await CreateAsync();
        await using var _db = db;

        var result = await service.SuggestAsync("Laptop", 500);

        Assert.True(result.Products.Count <= SearchService.MaxSuggestions);
    }

    [Fact]
    public async Task Search_UsesEngine_ForPlainQuery()
    {
        var (service, _, db) = await CreateAsync();
        await using var _db = db;

        var result = await service.SearchAsync("Laptop", new CatalogQuery { Page = 1, PageSize = 2 });

        Assert.Equal(SearchSource.SearchEngine, result.Source);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task Search_FallsBackToDatabase_WhenFiltersArePresent()
    {
        var (service, index, db) = await CreateAsync();
        await using var _db = db;

        var query = new CatalogQuery { Page = 1, PageSize = 24 };
        query.Attributes["processor"] = new List<string> { "Intel Core i7" };

        var result = await service.SearchAsync("Laptop", query);

        // The engine must not even be consulted: attribute semantics live in Postgres.
        Assert.Empty(index.Queries);
        Assert.Equal(SearchSource.Database, result.Source);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Search_FallsBackToDatabase_WhenEngineIsDown()
    {
        var (service, _, db) = await CreateAsync(engineUp: false);
        await using var _db = db;

        var result = await service.SearchAsync("Laptop", new CatalogQuery { Page = 1, PageSize = 24 });

        Assert.Equal(SearchSource.Database, result.Source);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task Search_WithBlankQuery_ListsEverythingFromDatabase()
    {
        var (service, _, db) = await CreateAsync();
        await using var _db = db;

        var result = await service.SearchAsync(null, new CatalogQuery { Page = 1, PageSize = 24 });

        Assert.Equal(SearchSource.Database, result.Source);
        // All Active products across every category; the Draft one is excluded.
        Assert.Equal(6, result.TotalCount);
    }
}

public class SearchIndexingServiceTests
{
    [Fact]
    public async Task ReindexAll_IndexesOnlyActiveProducts_AndClearsFirst()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var index = new FakeSearchIndex();
        var service = new SearchIndexingService(db, index);

        var count = await service.ReindexAllAsync();

        Assert.Equal(6, count);
        Assert.Equal(1, index.ResetCount);
        Assert.Equal(6, index.Documents.Count);
        Assert.DoesNotContain(index.Documents.Values, d => d.Slug == "draft-laptop");
    }

    [Fact]
    public async Task IndexProduct_ProjectsNameSlugBrandCategoryPriceAndAttributes()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var index = new FakeSearchIndex();
        var product = await db.Products.FirstAsync(p => p.Slug == "laptop-b");

        await new SearchIndexingService(db, index).IndexProductAsync(product.Id);

        var document = Assert.Single(index.Documents.Values);
        Assert.Equal("Laptop B", document.Name);
        Assert.Equal("laptop-b", document.Slug);
        Assert.Equal("Asus", document.BrandName);
        Assert.Equal("laptops", document.CategorySlug);
        Assert.Equal(200000m, document.Price);
        Assert.Equal(180000m, document.SalePrice);
        Assert.Equal("16GB", document.Attributes["ram"]);
        // Attribute values are flattened so they are full-text searchable.
        Assert.Contains("Intel Core i7", document.AttributesText);
    }

    [Fact]
    public async Task IndexProduct_RemovesDocument_WhenProductIsNotActive()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var index = new FakeSearchIndex();
        var service = new SearchIndexingService(db, index);
        await service.ReindexAllAsync();

        var product = await db.Products.FirstAsync(p => p.Slug == "laptop-a");
        product.Status = Domain.ProductStatus.Draft;
        await db.SaveChangesAsync();

        await service.IndexProductAsync(product.Id);

        Assert.DoesNotContain(index.Documents.Values, d => d.Slug == "laptop-a");
    }

    [Fact]
    public async Task IndexProduct_RemovesDocument_WhenProductIsGone()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var index = new FakeSearchIndex();
        var service = new SearchIndexingService(db, index);
        await service.ReindexAllAsync();
        var before = index.Documents.Count;

        await service.IndexProductAsync(Guid.NewGuid());

        Assert.Equal(before, index.Documents.Count);
    }

    [Fact]
    public async Task Indexing_IsANoOp_WhenTheEngineIsUnavailable()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var index = new FakeSearchIndex { Available = false };

        // Must not throw: a dead search engine may never fail an admin write.
        var count = await new SearchIndexingService(db, index).ReindexAllAsync();

        Assert.Equal(6, count);
        Assert.Empty(index.Documents);
    }
}
