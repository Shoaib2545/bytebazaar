namespace ByteBazaar.Application.Abstractions;

/// <summary>
/// Evicts ASP.NET Core output-cache entries by tag after admin catalog writes. Implemented in the
/// Api composition root (the output cache lives there); a no-op when output caching is disabled.
/// </summary>
public interface IOutputCacheInvalidator
{
    Task EvictAsync(params string[] tags);
}

/// <summary>Output-cache tags shared between the cache policies and the writers that evict them.</summary>
public static class CacheTags
{
    /// <summary>Everything served from the public catalog (tree, filters, product lists, search).</summary>
    public const string Catalog = "catalog";
}
