using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ByteBazaar.Api.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly CatalogService _catalogService;

    public CatalogController(CatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet("categories/tree")]
    [OutputCache(PolicyName = CachePolicies.CategoryTree)]
    public async Task<ActionResult<List<CategoryTreeDto>>> GetCategoryTree(CancellationToken ct)
    {
        return Ok(await _catalogService.GetCategoryTreeAsync(ct));
    }

    /// <summary>
    /// Filter definitions + option counts for a category. Heavy (it materializes the whole
    /// subtree's product projection), so it is output-cached per slug.
    /// </summary>
    [HttpGet("categories/{slug}/filters")]
    [OutputCache(PolicyName = CachePolicies.CatalogFilters)]
    public async Task<ActionResult<CategoryFiltersDto>> GetCategoryFilters(string slug, CancellationToken ct)
    {
        var filters = await _catalogService.GetCategoryFiltersAsync(slug, ct);
        return filters is null ? NotFound() : Ok(filters);
    }

    /// <summary>
    /// Filtered product listing. Output-cached with the full query string in the key, so every
    /// distinct filter combination is a distinct cache entry.
    /// </summary>
    [HttpGet("categories/{slug}/products")]
    [OutputCache(PolicyName = CachePolicies.CatalogProducts)]
    public async Task<ActionResult<PagedResultDto<ProductListItemDto>>> GetCategoryProducts(
        string slug, CancellationToken ct)
    {
        var result = await _catalogService.GetCategoryProductsAsync(slug, CatalogQueryBinder.FromRequest(Request), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Featured products (Active + isFeatured, newest first) for the home page.</summary>
    [HttpGet("featured")]
    [OutputCache(PolicyName = CachePolicies.CatalogProducts)]
    public async Task<ActionResult<List<ProductListItemDto>>> GetFeatured(
        [FromQuery] int count = 8, CancellationToken ct = default)
        => Ok(await _catalogService.GetFeaturedAsync(count, ct));

    [HttpGet("products/{slug}")]
    public async Task<ActionResult<ProductDetailDto>> GetProduct(string slug, CancellationToken ct)
    {
        var product = await _catalogService.GetProductAsync(slug, ct);
        return product is null ? NotFound() : Ok(product);
    }

    // GET /api/catalog/search was M3's Postgres full-text endpoint. M6's /api/search supersedes it
    // (Meilisearch, with this same query as its fallback) and is the only one the storefront calls,
    // so the route is retired. CatalogService.SearchAsync is NOT dead code — SearchService calls it
    // whenever Meilisearch is unreachable or unconfigured.
}
