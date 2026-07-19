using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private static readonly string[] ReservedQueryKeys = { "page", "pagesize", "sort", "brand", "price", "q" };

    private readonly CatalogService _catalogService;

    public CatalogController(CatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet("categories/tree")]
    public async Task<ActionResult<List<CategoryTreeDto>>> GetCategoryTree(CancellationToken ct)
    {
        return Ok(await _catalogService.GetCategoryTreeAsync(ct));
    }

    [HttpGet("categories/{slug}/filters")]
    public async Task<ActionResult<CategoryFiltersDto>> GetCategoryFilters(string slug, CancellationToken ct)
    {
        var filters = await _catalogService.GetCategoryFiltersAsync(slug, ct);
        return filters is null ? NotFound() : Ok(filters);
    }

    [HttpGet("categories/{slug}/products")]
    public async Task<ActionResult<PagedResultDto<ProductListItemDto>>> GetCategoryProducts(
        string slug, CancellationToken ct)
    {
        var result = await _catalogService.GetCategoryProductsAsync(slug, BuildQuery(), ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Featured products (Active + isFeatured, newest first) for the home page.</summary>
    [HttpGet("featured")]
    public async Task<ActionResult<List<ProductListItemDto>>> GetFeatured(
        [FromQuery] int count = 8, CancellationToken ct = default)
        => Ok(await _catalogService.GetFeaturedAsync(count, ct));

    [HttpGet("products/{slug}")]
    public async Task<ActionResult<ProductDetailDto>> GetProduct(string slug, CancellationToken ct)
    {
        var product = await _catalogService.GetProductAsync(slug, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet("search")]
    public async Task<ActionResult<PagedResultDto<ProductListItemDto>>> Search(
        [FromQuery] string? q, CancellationToken ct)
    {
        return Ok(await _catalogService.SearchAsync(q, BuildQuery(), ct));
    }

    private CatalogQuery BuildQuery()
    {
        var query = new CatalogQuery();

        if (int.TryParse(Request.Query["page"], out var page)) query.Page = page;
        if (int.TryParse(Request.Query["pageSize"], out var pageSize)) query.PageSize = pageSize;

        var sort = Request.Query["sort"].ToString();
        if (!string.IsNullOrWhiteSpace(sort)) query.Sort = sort.Trim().ToLowerInvariant();

        var brand = Request.Query["brand"].ToString();
        if (!string.IsNullOrWhiteSpace(brand))
            query.Brands = SplitValues(brand);

        var price = Request.Query["price"].ToString();
        if (!string.IsNullOrWhiteSpace(price))
        {
            var parts = price.Split('-', 2);
            if (parts.Length == 2)
            {
                if (decimal.TryParse(parts[0], out var min)) query.PriceMin = min;
                if (decimal.TryParse(parts[1], out var max)) query.PriceMax = max;
            }
        }

        foreach (var (key, values) in Request.Query)
        {
            if (ReservedQueryKeys.Contains(key.ToLowerInvariant())) continue;
            var merged = SplitValues(string.Join(',', values.Where(v => !string.IsNullOrWhiteSpace(v))!));
            if (merged.Count > 0)
                query.Attributes[key] = merged;
        }

        return query;
    }

    private static List<string> SplitValues(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
