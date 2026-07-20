namespace ByteBazaar.Application.DTOs;

/// <summary>Which backend answered a search request — useful when Meilisearch is down.</summary>
public enum SearchSource
{
    Database,
    SearchEngine
}

/// <summary>A single product row in the search-as-you-type dropdown.</summary>
public class ProductSuggestionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public string? ImageUrl { get; set; }
    public string? BrandName { get; set; }
    public string CategorySlug { get; set; } = string.Empty;
}

/// <summary>A category or brand shortcut in the suggest dropdown.</summary>
public class TermSuggestionDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public class SuggestResponseDto
{
    public string Query { get; set; } = string.Empty;
    public List<ProductSuggestionDto> Products { get; set; } = new();
    public List<TermSuggestionDto> Categories { get; set; } = new();
    public List<TermSuggestionDto> Brands { get; set; } = new();
    public int TotalProducts { get; set; }
    public SearchSource Source { get; set; } = SearchSource.Database;
}

public class SearchResultsDto
{
    public string Query { get; set; } = string.Empty;
    public List<ProductListItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public SearchSource Source { get; set; } = SearchSource.Database;
}
