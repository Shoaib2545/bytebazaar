namespace ByteBazaar.Application.Abstractions;

/// <summary>
/// The flattened shape pushed into the external search engine. Attributes are exposed both as a
/// dictionary (for faceting on dynamic attribute codes) and as a flattened text blob so the
/// engine's full-text index covers attribute values too.
/// </summary>
public class SearchProductDocument
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BrandName { get; set; }
    public string? BrandSlug { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public string? ImageUrl { get; set; }
    public int Stock { get; set; }
    public long CreatedAt { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
    public string AttributesText { get; set; } = string.Empty;
}

public class SearchHit
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public string? ImageUrl { get; set; }
    public string? BrandName { get; set; }
    public string CategorySlug { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int Stock { get; set; }
}

public class SearchIndexResult
{
    public List<SearchHit> Hits { get; set; } = new();
    public int TotalCount { get; set; }
}

/// <summary>
/// Application-facing abstraction over the external search engine (Meilisearch in Infrastructure).
/// Every member degrades gracefully: writes are best-effort and never throw, and
/// <see cref="SearchAsync"/> returns <c>null</c> when the engine is unreachable or unconfigured so
/// callers can fall back to the database.
/// </summary>
public interface ISearchIndex
{
    /// <summary>Upserts documents into the product index. Never throws.</summary>
    Task IndexProductsAsync(IReadOnlyList<SearchProductDocument> documents, CancellationToken ct = default);

    /// <summary>Removes a product document by id. Never throws.</summary>
    Task DeleteProductAsync(Guid productId, CancellationToken ct = default);

    /// <summary>Clears the product index before a full rebuild. Never throws.</summary>
    Task ResetProductsAsync(CancellationToken ct = default);

    /// <summary>
    /// Full-text search. Returns <c>null</c> when the engine is unavailable, which callers must
    /// treat as "fall back to the database" rather than as an empty result set.
    /// </summary>
    Task<SearchIndexResult?> SearchAsync(string query, int offset, int limit, CancellationToken ct = default);
}
