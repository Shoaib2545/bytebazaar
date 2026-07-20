using ByteBazaar.Application.Abstractions;

namespace ByteBazaar.Tests;

public class FakeNotificationQueue : IOrderNotificationQueue
{
    public List<(string OrderNumber, string Email)> Placed { get; } = new();
    public List<(string OrderNumber, string Status, string Email)> StatusChanges { get; } = new();

    public Task EnqueueOrderPlacedAsync(string orderNumber, string email)
    {
        Placed.Add((orderNumber, email));
        return Task.CompletedTask;
    }

    public Task EnqueueStatusChangedAsync(string orderNumber, string status, string email)
    {
        StatusChanges.Add((orderNumber, status, email));
        return Task.CompletedTask;
    }
}

public class FakeRevalidator : IStorefrontRevalidator
{
    public List<string> Paths { get; } = new();

    public void Revalidate(params string[] paths) => Paths.AddRange(paths);
}

/// <summary>In-memory search index. <see cref="Available"/> = false emulates Meilisearch being down.</summary>
public class FakeSearchIndex : ISearchIndex
{
    public bool Available { get; set; } = true;
    public Dictionary<string, SearchProductDocument> Documents { get; } = new();
    public int ResetCount { get; private set; }
    public List<string> Queries { get; } = new();

    public Task IndexProductsAsync(IReadOnlyList<SearchProductDocument> documents, CancellationToken ct = default)
    {
        if (!Available) return Task.CompletedTask;
        foreach (var document in documents)
            Documents[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task DeleteProductAsync(Guid productId, CancellationToken ct = default)
    {
        if (Available) Documents.Remove(productId.ToString("N"));
        return Task.CompletedTask;
    }

    public Task ResetProductsAsync(CancellationToken ct = default)
    {
        if (!Available) return Task.CompletedTask;
        ResetCount++;
        Documents.Clear();
        return Task.CompletedTask;
    }

    public Task<SearchIndexResult?> SearchAsync(string query, int offset, int limit, CancellationToken ct = default)
    {
        Queries.Add(query);
        // Contract: null means "engine unavailable, fall back to the database".
        if (!Available) return Task.FromResult<SearchIndexResult?>(null);

        var matches = Documents.Values
            .Where(d => d.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Name)
            .ToList();

        return Task.FromResult<SearchIndexResult?>(new SearchIndexResult
        {
            TotalCount = matches.Count,
            Hits = matches.Skip(offset).Take(limit).Select(d => new SearchHit
            {
                Id = Guid.Parse(d.Id),
                Name = d.Name,
                Slug = d.Slug,
                Price = d.Price,
                SalePrice = d.SalePrice,
                ImageUrl = d.ImageUrl,
                BrandName = d.BrandName,
                CategorySlug = d.CategorySlug,
                CategoryName = d.CategoryName,
                Stock = d.Stock
            }).ToList()
        });
    }
}

public class FakeSearchIndexQueue : ISearchIndexQueue
{
    public List<Guid> Indexed { get; } = new();
    public List<Guid> Deleted { get; } = new();
    public int FullReindexes { get; private set; }

    public Task EnqueueProductIndexAsync(Guid productId)
    {
        Indexed.Add(productId);
        return Task.CompletedTask;
    }

    public Task EnqueueProductDeleteAsync(Guid productId)
    {
        Deleted.Add(productId);
        return Task.CompletedTask;
    }

    public Task EnqueueFullReindexAsync()
    {
        FullReindexes++;
        return Task.CompletedTask;
    }
}

/// <summary>Dictionary-backed cache that records hits, misses and evictions.</summary>
public class FakeCacheStore : ICacheStore
{
    private readonly Dictionary<string, object?> _entries = new();

    public int Hits { get; private set; }
    public int Misses { get; private set; }
    public List<string> Removed { get; } = new();

    public async Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default)
    {
        if (_entries.TryGetValue(key, out var cached) && cached is T typed)
        {
            Hits++;
            return typed;
        }

        Misses++;
        var value = await factory(ct);
        _entries[key] = value;
        return value;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        if (_entries.Remove(key)) Removed.Add(key);
        return Task.CompletedTask;
    }
}

public class FakeOutputCacheInvalidator : IOutputCacheInvalidator
{
    public List<string> EvictedTags { get; } = new();

    public Task EvictAsync(params string[] tags)
    {
        EvictedTags.AddRange(tags);
        return Task.CompletedTask;
    }
}
