using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using ByteBazaar.Domain;
using ByteBazaar.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ByteBazaar.Tests;

public class CatalogCachingTests
{
    [Fact]
    public async Task GetCategoryTree_IsServedFromCacheOnSecondCall()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var cache = new FakeCacheStore();
        var service = new CatalogService(db, cache);

        var first = await service.GetCategoryTreeAsync();
        var second = await service.GetCategoryTreeAsync();

        Assert.Equal(1, cache.Misses);
        Assert.Equal(1, cache.Hits);
        Assert.Equal(first.Count, second.Count);
    }

    [Fact]
    public async Task GetFeatured_CachesPerRequestedCount()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var product = await db.Products.FirstAsync(p => p.Slug == "laptop-a");
        product.IsFeatured = true;
        await db.SaveChangesAsync();

        var cache = new FakeCacheStore();
        var service = new CatalogService(db, cache);

        await service.GetFeaturedAsync(4);
        await service.GetFeaturedAsync(4);
        await service.GetFeaturedAsync(8);

        Assert.Equal(2, cache.Misses);
        Assert.Equal(1, cache.Hits);
    }

    [Fact]
    public async Task ActiveBanners_AreCached_AndEvictedOnAdminWrite()
    {
        await using var db = TestDbFactory.Create();
        var cache = new FakeCacheStore();
        var service = new BannerService(db, cache);

        await service.GetActiveBannersAsync();
        await service.GetActiveBannersAsync();
        Assert.Equal(1, cache.Misses);
        Assert.Equal(1, cache.Hits);

        await service.CreateAsync(new BannerUpsertRequest
        {
            Title = "Sale", ImageUrl = "https://img/hero.png", IsActive = true
        });

        Assert.Contains(CacheKeys.HomeBanners, cache.Removed);
        var banners = await service.GetActiveBannersAsync();
        Assert.Single(banners);
    }

    [Fact]
    public async Task CatalogService_WithoutACache_StillWorks()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CatalogService(db);

        var tree = await service.GetCategoryTreeAsync();

        Assert.Equal(2, tree.Count);
    }

    /// <summary>
    /// The tree is cached as JSON in Redis, so the SEO fields have to survive a real serialize /
    /// deserialize round trip under the camelCase web defaults the cache store uses.
    /// </summary>
    [Fact]
    public async Task CategoryTreeSeoMetadata_SurvivesTheDistributedCacheRoundTrip()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var laptops = await db.Categories.FirstAsync(c => c.Id == TestDbFactory.LaptopsId);
        laptops.MetaTitle = "Laptops | ByteBazaar";
        laptops.MetaDescription = "Gaming and business laptops.";
        await db.SaveChangesAsync();

        var distributed = new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        var store = new DistributedCacheStore(
            distributed, new MemoryCache(new MemoryCacheOptions()), NullLogger<DistributedCacheStore>.Instance);
        var service = new CatalogService(db, store);

        await service.GetCategoryTreeAsync();          // populates the cache
        var cached = await service.GetCategoryTreeAsync(); // served from the cached JSON

        var node = cached.Single(c => c.Slug == "laptops");
        Assert.Equal("Laptops | ByteBazaar", node.MetaTitle);
        Assert.Equal("Gaming and business laptops.", node.MetaDescription);

        var payload = await distributed.GetStringAsync(CacheKeys.CategoryTree);
        Assert.Contains("\"metaTitle\":\"Laptops | ByteBazaar\"", payload);
    }

    /// <summary>
    /// A tree payload cached before these fields existed has no metaTitle/metaDescription. It must
    /// still deserialize (as nulls) rather than throwing or serving a broken node.
    /// </summary>
    [Fact]
    public async Task LegacyCachedTreePayload_WithoutSeoFields_DeserializesWithNulls()
    {
        var distributed = new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        await distributed.SetStringAsync(
            CacheKeys.CategoryTree,
            """[{"id":"11111111-1111-1111-1111-111111111111","name":"Laptops","slug":"laptops","imageUrl":null,"sortOrder":1,"children":[]}]""");

        var store = new DistributedCacheStore(
            distributed, new MemoryCache(new MemoryCacheOptions()), NullLogger<DistributedCacheStore>.Instance);

        var tree = await store.GetOrSetAsync<List<CategoryTreeDto>>(
            CacheKeys.CategoryTree, _ => throw new InvalidOperationException("must be served from cache"),
            TimeSpan.FromMinutes(10));

        var node = Assert.Single(tree);
        Assert.Equal("laptops", node.Slug);
        Assert.Null(node.MetaTitle);
        Assert.Null(node.MetaDescription);
    }
}

public class AdminCacheInvalidationTests
{
    private static (AdminCatalogService Service, FakeCacheStore Cache, FakeOutputCacheInvalidator Output, FakeSearchIndexQueue Queue)
        Create(IAppDbContext db)
    {
        var cache = new FakeCacheStore();
        var output = new FakeOutputCacheInvalidator();
        var queue = new FakeSearchIndexQueue();
        return (new AdminCatalogService(db, new FakeRevalidator(), cache, output, queue), cache, output, queue);
    }

    [Fact]
    public async Task CreateCategory_EvictsCategoryTreeAndCatalogOutputCache()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (service, cache, output, _) = Create(db);
        // Warm the tree so there is something to evict.
        await new CatalogService(db, cache).GetCategoryTreeAsync();

        await service.CreateCategoryAsync(new CategoryUpsertRequest
        {
            Name = "Monitors", Slug = "monitors", SortOrder = 9, IsActive = true
        });

        Assert.Contains(CacheKeys.CategoryTree, cache.Removed);
        Assert.Contains(CacheTags.Catalog, output.EvictedTags);
    }

    /// <summary>
    /// Editing only the SEO fields changes the cached tree payload, so it must evict the tree —
    /// otherwise the admin's new metadata would not reach the storefront until the TTL lapsed.
    /// </summary>
    [Fact]
    public async Task UpdateCategorySeoOnly_EvictsTheTree_AndTheNextReadSeesTheNewMetadata()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (service, cache, output, _) = Create(db);
        var catalog = new CatalogService(db, cache);

        var warm = await catalog.GetCategoryTreeAsync();
        Assert.Null(warm.Single(c => c.Slug == "laptops").MetaTitle);

        var existing = await db.Categories.FirstAsync(c => c.Id == TestDbFactory.LaptopsId);
        db.ChangeTracker.Clear();

        await service.UpdateCategoryAsync(existing.Id, new CategoryUpsertRequest
        {
            Name = existing.Name,
            Slug = existing.Slug,
            ParentId = existing.ParentId,
            SortOrder = existing.SortOrder,
            IsActive = true,
            MetaTitle = "Best Laptops | ByteBazaar",
            MetaDescription = "Compare laptops by processor and RAM."
        });

        Assert.Contains(CacheKeys.CategoryTree, cache.Removed);
        Assert.Contains(CacheTags.Catalog, output.EvictedTags);

        var refreshed = await catalog.GetCategoryTreeAsync();
        var node = refreshed.Single(c => c.Slug == "laptops");
        Assert.Equal("Best Laptops | ByteBazaar", node.MetaTitle);
        Assert.Equal("Compare laptops by processor and RAM.", node.MetaDescription);
    }

    [Fact]
    public async Task CreateProduct_QueuesReindexAndEvictsCatalogCaches()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (service, _, output, queue) = Create(db);

        var created = await service.CreateProductAsync(new ProductUpsertRequest
        {
            Name = "New Laptop",
            Slug = "new-laptop",
            CategoryId = TestDbFactory.LaptopsId,
            Price = 111000m,
            Stock = 2,
            Status = ProductStatus.Active
        });

        Assert.Contains(created.Id, queue.Indexed);
        Assert.Contains(CacheTags.Catalog, output.EvictedTags);
    }

    [Fact]
    public async Task UpdateProduct_QueuesReindex()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var product = await db.Products.FirstAsync(p => p.Slug == "laptop-a");
        db.ChangeTracker.Clear();
        var (service, _, _, queue) = Create(db);

        await service.UpdateProductAsync(product.Id, new ProductUpsertRequest
        {
            Name = product.Name,
            Slug = product.Slug,
            CategoryId = product.CategoryId,
            Price = product.Price,
            Stock = product.Stock,
            Status = ProductStatus.Active
        });

        Assert.Contains(product.Id, queue.Indexed);
    }

    [Fact]
    public async Task DeleteProduct_QueuesDocumentDeletion()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var product = await db.Products.FirstAsync(p => p.Slug == "laptop-e");
        db.ChangeTracker.Clear();
        var (service, _, _, queue) = Create(db);

        await service.DeleteProductAsync(product.Id);

        Assert.Contains(product.Id, queue.Deleted);
        Assert.Empty(queue.Indexed);
    }

    [Fact]
    public async Task BrandWrite_EvictsCatalogOutputCache()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var (service, _, output, _) = Create(db);

        await service.CreateBrandAsync(new BrandUpsertRequest { Name = "Gigabyte", Slug = "gigabyte" });

        Assert.Contains(CacheTags.Catalog, output.EvictedTags);
    }
}

public class DistributedCacheStoreTests
{
    private static DistributedCacheStore Create(IDistributedCache distributed) =>
        new(distributed, new MemoryCache(new MemoryCacheOptions()), NullLogger<DistributedCacheStore>.Instance);

    [Fact]
    public async Task UsesTheDistributedCache_WhenItWorks()
    {
        var store = Create(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));

        var calls = 0;
        Task<List<string>> Factory(CancellationToken ct)
        {
            calls++;
            return Task.FromResult(new List<string> { "a", "b" });
        }

        var first = await store.GetOrSetAsync("k", Factory, TimeSpan.FromMinutes(1));
        var second = await store.GetOrSetAsync("k", Factory, TimeSpan.FromMinutes(1));

        Assert.Equal(1, calls);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task FallsBackToMemory_WhenRedisThrows()
    {
        var store = Create(new ThrowingDistributedCache());

        var calls = 0;
        Task<string> Factory(CancellationToken ct)
        {
            calls++;
            return Task.FromResult("value");
        }

        var first = await store.GetOrSetAsync("k", Factory, TimeSpan.FromMinutes(1));
        var second = await store.GetOrSetAsync("k", Factory, TimeSpan.FromMinutes(1));

        // First call trips the breaker and computes; second is served from the memory fallback.
        Assert.Equal("value", first);
        Assert.Equal("value", second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Remove_DoesNotThrow_WhenRedisIsDown()
    {
        var store = Create(new ThrowingDistributedCache());
        await store.RemoveAsync("k");
    }

    /// <summary>Stands in for a Redis server that is not reachable.</summary>
    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("redis down");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("redis down");
        public void Refresh(string key) => throw new InvalidOperationException("redis down");
        public Task RefreshAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("redis down");
        public void Remove(string key) => throw new InvalidOperationException("redis down");
        public Task RemoveAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("redis down");
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new InvalidOperationException("redis down");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw new InvalidOperationException("redis down");
    }
}
