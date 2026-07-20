using ByteBazaar.Application.Abstractions;

namespace ByteBazaar.Application.Services;

/// <summary>
/// Pass-through cache used when no cache is configured (unit tests, or a host that deliberately
/// opts out). Always calls the factory; never stores anything.
/// </summary>
public sealed class NoOpCacheStore : ICacheStore
{
    public static readonly NoOpCacheStore Instance = new();

    public Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default)
        => factory(ct);

    public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>No-op output-cache invalidator for hosts without ASP.NET Core output caching.</summary>
public sealed class NoOpOutputCacheInvalidator : IOutputCacheInvalidator
{
    public static readonly NoOpOutputCacheInvalidator Instance = new();

    public Task EvictAsync(params string[] tags) => Task.CompletedTask;
}

/// <summary>No-op search-index queue: used by tests and when indexing is not configured.</summary>
public sealed class NoOpSearchIndexQueue : ISearchIndexQueue
{
    public static readonly NoOpSearchIndexQueue Instance = new();

    public Task EnqueueProductIndexAsync(Guid productId) => Task.CompletedTask;
    public Task EnqueueProductDeleteAsync(Guid productId) => Task.CompletedTask;
    public Task EnqueueFullReindexAsync() => Task.CompletedTask;
}
