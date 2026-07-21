using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Tests.Integration;

/// <summary>
/// Provider parity for the dynamic attribute engine (CLAUDE.md §4): the attribute predicate has
/// two implementations — jsonb containment (<c>@&gt;</c>) on Npgsql and dictionary evaluation on
/// InMemory — and until now only the InMemory branch was ever executed by a test. These mirror
/// CatalogServiceTests case for case against real PostgreSQL, so a change that breaks only the
/// Npgsql expression tree can no longer pass CI.
/// </summary>
[Collection(PostgresCollection.Name)]
public class JsonbFilterIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private string _database = string.Empty;

    public JsonbFilterIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _database = await _fixture.CreateDatabaseAsync();
        await using var db = _fixture.CreateContext(_database);
        await IntegrationSeed.SeedCatalogAsync(db);

        await IntegrationSeed.AddProductAsync(db, "Laptop A", "laptop-a", 100000m, 5,
            new() { ["processor"] = "Intel Core i5", ["ram"] = "8GB" });
        await IntegrationSeed.AddProductAsync(db, "Laptop B", "laptop-b", 200000m, 5,
            new() { ["processor"] = "Intel Core i7", ["ram"] = "16GB" });
        await IntegrationSeed.AddProductAsync(db, "Laptop C", "laptop-c", 300000m, 5,
            new() { ["processor"] = "Intel Core i7", ["ram"] = "32GB" });
        await IntegrationSeed.AddProductAsync(db, "Laptop D", "laptop-d", 150000m, 5,
            new() { ["processor"] = "AMD Ryzen 5", ["ram"] = "16GB" });
        // No attributes at all — the null/missing-key case that made the InMemory branch need an
        // explicit ContainsKey guard. jsonb containment must simply not match it.
        await IntegrationSeed.AddProductAsync(db, "Laptop Bare", "laptop-bare", 90000m, 5);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private CatalogService Service(out AppDbContextHandle handle)
    {
        var db = _fixture.CreateContext(_database);
        handle = new AppDbContextHandle(db);
        return new CatalogService(db);
    }

    [Fact]
    public async Task SingleAttribute_UsesJsonbContainment()
    {
        var service = Service(out var handle);
        await using var _ = handle;

        var query = new CatalogQuery();
        query.Attributes["processor"] = new List<string> { "Intel Core i7" };

        var result = await service.GetCategoryProductsAsync("laptops", query);

        Assert.NotNull(result);
        Assert.Equal(2, result!.TotalCount);
        Assert.All(result.Items, i => Assert.Contains(i.Slug, new[] { "laptop-b", "laptop-c" }));
    }

    [Fact]
    public async Task TwoAttributes_AreAndedAcross()
    {
        var service = Service(out var handle);
        await using var _ = handle;

        var query = new CatalogQuery();
        query.Attributes["processor"] = new List<string> { "Intel Core i7" };
        query.Attributes["ram"] = new List<string> { "16GB" };

        var result = await service.GetCategoryProductsAsync("laptops", query);

        Assert.NotNull(result);
        Assert.Equal("laptop-b", Assert.Single(result!.Items).Slug);
    }

    [Fact]
    public async Task MultipleValuesWithinAnAttribute_AreOred()
    {
        var service = Service(out var handle);
        await using var _ = handle;

        var query = new CatalogQuery();
        query.Attributes["ram"] = new List<string> { "16GB", "32GB" };

        var result = await service.GetCategoryProductsAsync("laptops", query);

        Assert.NotNull(result);
        Assert.Equal(3, result!.TotalCount);
        Assert.All(result.Items, i => Assert.Contains(i.Slug, new[] { "laptop-b", "laptop-c", "laptop-d" }));
    }

    /// <summary>The empty-jsonb row must never match a containment filter (and must not throw).</summary>
    [Fact]
    public async Task ProductWithNoAttributes_IsExcludedByAnyAttributeFilter()
    {
        var service = Service(out var handle);
        await using var _ = handle;

        var query = new CatalogQuery();
        query.Attributes["ram"] = new List<string> { "8GB" };

        var result = await service.GetCategoryProductsAsync("laptops", query);

        Assert.NotNull(result);
        Assert.DoesNotContain(result!.Items, i => i.Slug == "laptop-bare");
        Assert.Equal("laptop-a", Assert.Single(result.Items).Slug);
    }

    [Fact]
    public async Task UnknownAttributeValue_ReturnsEmpty()
    {
        var service = Service(out var handle);
        await using var _ = handle;

        var query = new CatalogQuery();
        query.Attributes["ram"] = new List<string> { "128GB" };

        var result = await service.GetCategoryProductsAsync("laptops", query);

        Assert.NotNull(result);
        Assert.Equal(0, result!.TotalCount);
    }

    /// <summary>
    /// The filter sidebar's option counts come from the same jsonb path. A count that disagrees
    /// with the listing is the classic provider-parity bug, so assert them together.
    /// </summary>
    [Fact]
    public async Task FilterOptionCounts_MatchTheListingResults()
    {
        var service = Service(out var handle);
        await using var _ = handle;

        var filters = await service.GetCategoryFiltersAsync("laptops");
        Assert.NotNull(filters);

        var ram = filters!.Attributes.Single(a => a.Code == "ram");
        foreach (var option in ram.Options.Where(o => o.Count > 0))
        {
            var query = new CatalogQuery();
            query.Attributes["ram"] = new List<string> { option.Value };
            var listing = await service.GetCategoryProductsAsync("laptops", query);

            Assert.NotNull(listing);
            Assert.Equal(option.Count, listing!.TotalCount);
        }
    }

    /// <summary>
    /// Proves the GIN index from AppDbContext.OnModelCreating actually exists in the migrated
    /// schema. Without it these queries still return correct rows — they just seq-scan, which is a
    /// silent performance cliff no functional test would ever catch.
    /// </summary>
    [Fact]
    public async Task ProductsAttributes_HasAGinIndex()
    {
        await using var db = _fixture.CreateContext(_database);
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT count(*)
            FROM pg_index i
            JOIN pg_class idx  ON idx.oid = i.indexrelid
            JOIN pg_class tbl  ON tbl.oid = i.indrelid
            JOIN pg_am   am    ON am.oid  = idx.relam
            WHERE tbl.relname = 'Products' AND am.amname = 'gin'
            """;

        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        Assert.True(count >= 1, "No GIN index on Products — the dynamic filter engine would seq-scan.");
    }
}

/// <summary>Lets a test dispose the context created alongside a service in one line.</summary>
public sealed class AppDbContextHandle : IAsyncDisposable
{
    private readonly ByteBazaar.Infrastructure.Persistence.AppDbContext _db;

    public AppDbContextHandle(ByteBazaar.Infrastructure.Persistence.AppDbContext db) => _db = db;

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}
