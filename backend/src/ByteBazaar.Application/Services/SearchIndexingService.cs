using ByteBazaar.Application.Abstractions;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

/// <summary>
/// Projects products into <see cref="SearchProductDocument"/>s and pushes them at
/// <see cref="ISearchIndex"/>. These methods are the Hangfire job bodies (see
/// Infrastructure/Search/SearchIndexQueue.cs) — they are safe to run when the search engine is
/// down because every <see cref="ISearchIndex"/> member degrades to a no-op.
/// </summary>
public class SearchIndexingService
{
    /// <summary>Batch size for the full rebuild; keeps a single HTTP payload reasonable.</summary>
    private const int ReindexBatchSize = 200;

    private readonly IAppDbContext _db;
    private readonly ISearchIndex _index;

    public SearchIndexingService(IAppDbContext db, ISearchIndex index)
    {
        _db = db;
        _index = index;
    }

    /// <summary>
    /// Indexes one product. A product that no longer exists — or is no longer Active — is removed
    /// from the index instead, so unpublishing behaves like deleting for search purposes.
    /// </summary>
    public async Task IndexProductAsync(Guid productId, CancellationToken ct = default)
    {
        var product = await LoadQuery()
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

        if (product is null || product.Status != ProductStatus.Active)
        {
            await _index.DeleteProductAsync(productId, ct);
            return;
        }

        await _index.IndexProductsAsync(new[] { ToDocument(product) }, ct);
    }

    public Task DeleteProductAsync(Guid productId, CancellationToken ct = default)
        => _index.DeleteProductAsync(productId, ct);

    /// <summary>Clears and rebuilds the whole product index from the database.</summary>
    public async Task<int> ReindexAllAsync(CancellationToken ct = default)
    {
        await _index.ResetProductsAsync(ct);

        var indexed = 0;
        var skip = 0;
        while (true)
        {
            var batch = await LoadQuery()
                .Where(p => p.Status == ProductStatus.Active)
                .OrderBy(p => p.Id)
                .Skip(skip)
                .Take(ReindexBatchSize)
                .ToListAsync(ct);
            if (batch.Count == 0) break;

            await _index.IndexProductsAsync(batch.Select(ToDocument).ToList(), ct);
            indexed += batch.Count;
            skip += batch.Count;
        }

        return indexed;
    }

    private IQueryable<Product> LoadQuery() => _db.Products.AsNoTracking()
        .Include(p => p.Brand)
        .Include(p => p.Category)
        .Include(p => p.Images);

    /// <summary>Visible for tests: the exact document shape pushed to the engine.</summary>
    public static SearchProductDocument ToDocument(Product product)
    {
        var now = DateTime.UtcNow;
        return new SearchProductDocument
        {
            Id = product.Id.ToString("N"),
            Name = product.Name,
            Slug = product.Slug,
            Description = product.Description,
            BrandName = product.Brand?.Name,
            BrandSlug = product.Brand?.Slug,
            CategoryName = product.Category?.Name ?? string.Empty,
            CategorySlug = product.Category?.Slug ?? string.Empty,
            Price = product.Price,
            SalePrice = ProductPricing.EffectiveSalePrice(product.SalePrice, product.SaleStart, product.SaleEnd, now),
            ImageUrl = product.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
            Stock = product.Stock,
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(product.CreatedAt, DateTimeKind.Utc)).ToUnixTimeSeconds(),
            Attributes = new Dictionary<string, string>(product.Attributes),
            // Flattened so attribute values ("32GB", "Intel Core i7") are full-text searchable.
            AttributesText = string.Join(' ', product.Attributes.Values)
        };
    }
}
