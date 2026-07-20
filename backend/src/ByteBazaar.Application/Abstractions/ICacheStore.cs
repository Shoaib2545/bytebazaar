namespace ByteBazaar.Application.Abstractions;

/// <summary>
/// Hot-data cache for read-mostly payloads (category tree, homepage). Backed by Redis when it is
/// reachable and by an in-process memory cache otherwise — callers never need to know which, and
/// no member ever throws because of a cache outage.
/// </summary>
public interface ICacheStore
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or invokes <paramref name="factory"/>,
    /// caches the result for <paramref name="ttl"/> and returns it. A cache failure falls through
    /// to <paramref name="factory"/>.
    /// </summary>
    Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Evicts a single key. Never throws.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);
}

/// <summary>Well-known cache keys, kept in one place so writers and readers cannot drift.</summary>
public static class CacheKeys
{
    public const string CategoryTree = "catalog:category-tree";
    public const string HomeBanners = "content:home-banners";

    public static string Featured(int count) => $"catalog:featured:{count}";
}
