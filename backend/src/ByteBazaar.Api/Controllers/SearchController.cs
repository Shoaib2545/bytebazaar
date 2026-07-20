using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers;

/// <summary>
/// Public search. Served by Meilisearch when it is reachable and by Postgres otherwise; the
/// <c>source</c> field on each response says which answered.
/// </summary>
[ApiController]
[Route("api/search")]
public class SearchController : ControllerBase
{
    private readonly SearchService _searchService;

    public SearchController(SearchService searchService)
    {
        _searchService = searchService;
    }

    /// <summary>
    /// Search-as-you-type payload for the header box: matching products plus category and brand
    /// shortcuts. Returns an empty payload (200) for a blank query rather than an error.
    /// </summary>
    [HttpGet("suggest")]
    public async Task<ActionResult<SuggestResponseDto>> Suggest(
        [FromQuery] string? q, [FromQuery] int limit = 5, CancellationToken ct = default)
        => Ok(await _searchService.SuggestAsync(q, limit, ct));

    /// <summary>
    /// Full search-results page. Accepts the same filter/sort/paging query params as the catalog
    /// endpoints (brand, price, sort, page, pageSize, plus arbitrary attribute codes).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SearchResultsDto>> Search(
        [FromQuery] string? q, CancellationToken ct = default)
        => Ok(await _searchService.SearchAsync(q, CatalogQueryBinder.FromRequest(Request), ct));
}
