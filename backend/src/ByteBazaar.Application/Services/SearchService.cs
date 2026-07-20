using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Domain;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

/// <summary>
/// Public search. Prefers the external search engine and transparently falls back to the
/// database when it is unreachable (<see cref="ISearchIndex.SearchAsync"/> returns null) or when
/// the request carries structural filters the engine query here does not model
/// (brand/price/attribute facets, explicit sorting) — those stay on Postgres via
/// <see cref="CatalogService"/> so filter semantics have exactly one implementation.
/// </summary>
public class SearchService
{
    public const int MaxSuggestions = 10;

    private readonly IAppDbContext _db;
    private readonly ISearchIndex _index;
    private readonly CatalogService _catalog;

    public SearchService(IAppDbContext db, ISearchIndex index, CatalogService catalog)
    {
        _db = db;
        _index = index;
        _catalog = catalog;
    }

    /// <summary>Search-as-you-type payload for the header: a few products plus category/brand shortcuts.</summary>
    public async Task<SuggestResponseDto> SuggestAsync(string? q, int limit, CancellationToken ct = default)
    {
        var term = (q ?? string.Empty).Trim();
        limit = Math.Clamp(limit <= 0 ? 5 : limit, 1, MaxSuggestions);

        var response = new SuggestResponseDto { Query = term };
        if (term.Length == 0) return response;

        var engine = await _index.SearchAsync(term, 0, limit, ct);
        if (engine is not null)
        {
            response.Source = SearchSource.SearchEngine;
            response.TotalProducts = engine.TotalCount;
            response.Products = engine.Hits.Select(h => new ProductSuggestionDto
            {
                Id = h.Id,
                Name = h.Name,
                Slug = h.Slug,
                Price = h.Price,
                SalePrice = h.SalePrice,
                ImageUrl = h.ImageUrl,
                BrandName = h.BrandName,
                CategorySlug = h.CategorySlug
            }).ToList();
        }
        else
        {
            var lowered = term.ToLower();
            var now = DateTime.UtcNow;
            var matches = _db.Products.AsNoTracking()
                .Where(p => p.Status == ProductStatus.Active && p.Name.ToLower().Contains(lowered));

            response.TotalProducts = await matches.CountAsync(ct);
            var products = await matches
                .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
                .Take(limit)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Slug,
                    p.Price,
                    p.SalePrice,
                    p.SaleStart,
                    p.SaleEnd,
                    BrandName = p.Brand != null ? p.Brand.Name : null,
                    CategorySlug = p.Category != null ? p.Category.Slug : null,
                    ImageUrl = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()
                })
                .ToListAsync(ct);

            response.Products = products.Select(p => new ProductSuggestionDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Price = p.Price,
                SalePrice = ProductPricing.EffectiveSalePrice(p.SalePrice, p.SaleStart, p.SaleEnd, now),
                ImageUrl = p.ImageUrl,
                BrandName = p.BrandName,
                CategorySlug = p.CategorySlug ?? string.Empty
            }).ToList();
        }

        // Category/brand shortcuts always come from the database: they are tiny tables and the
        // dropdown must still offer navigation when the engine is down.
        var loweredTerm = term.ToLower();
        response.Categories = await _db.Categories.AsNoTracking()
            .Where(c => c.IsActive && c.Name.ToLower().Contains(loweredTerm))
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Take(5)
            .Select(c => new TermSuggestionDto { Name = c.Name, Slug = c.Slug })
            .ToListAsync(ct);

        response.Brands = await _db.Brands.AsNoTracking()
            .Where(b => b.Name.ToLower().Contains(loweredTerm))
            .OrderBy(b => b.Name)
            .Take(5)
            .Select(b => new TermSuggestionDto { Name = b.Name, Slug = b.Slug })
            .ToListAsync(ct);

        return response;
    }

    /// <summary>Full search-results page. Honours the same filter/sort query params as the catalog.</summary>
    public async Task<SearchResultsDto> SearchAsync(string? q, CatalogQuery query, CancellationToken ct = default)
    {
        var term = (q ?? string.Empty).Trim();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        if (term.Length > 0 && !HasStructuralFilters(query))
        {
            var engine = await _index.SearchAsync(term, (page - 1) * pageSize, pageSize, ct);
            if (engine is not null)
            {
                return new SearchResultsDto
                {
                    Query = term,
                    Items = engine.Hits.Select(h => new ProductListItemDto
                    {
                        Id = h.Id,
                        Name = h.Name,
                        Slug = h.Slug,
                        Price = h.Price,
                        SalePrice = h.SalePrice,
                        ImageUrl = h.ImageUrl,
                        BrandName = h.BrandName,
                        Stock = h.Stock
                    }).ToList(),
                    TotalCount = engine.TotalCount,
                    Page = page,
                    PageSize = pageSize,
                    Source = SearchSource.SearchEngine
                };
            }
        }

        var fallback = await _catalog.SearchAsync(term.Length == 0 ? null : term, query, ct);
        return new SearchResultsDto
        {
            Query = term,
            Items = fallback.Items,
            TotalCount = fallback.TotalCount,
            Page = fallback.Page,
            PageSize = fallback.PageSize,
            Source = SearchSource.Database
        };
    }

    /// <summary>True when the request needs Postgres-side filter composition or explicit sorting.</summary>
    private static bool HasStructuralFilters(CatalogQuery query) =>
        query.Brands.Count > 0
        || query.PriceMin is not null
        || query.PriceMax is not null
        || query.Attributes.Any(a => a.Value.Count > 0)
        || (!string.IsNullOrWhiteSpace(query.Sort) && query.Sort != "relevance");
}
