namespace ByteBazaar.Application.DTOs;

public class CategoryTreeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public List<CategoryTreeDto> Children { get; set; } = new();
}

public class CategoryFiltersDto
{
    public List<AttributeFilterDto> Attributes { get; set; } = new();
    public List<BrandFilterDto> Brands { get; set; } = new();
    public PriceRangeDto PriceRange { get; set; } = new();
}

public class AttributeFilterDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Widget { get; set; } = string.Empty;
    public List<FilterOptionDto> Options { get; set; } = new();
}

public class FilterOptionDto
{
    public string Value { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class BrandFilterDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class PriceRangeDto
{
    public decimal Min { get; set; }
    public decimal Max { get; set; }
}

public class ProductListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public string? ImageUrl { get; set; }
    public string? BrandName { get; set; }
    public int Stock { get; set; }
}

public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ProductAttributeDto
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class ProductDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public int Stock { get; set; }
    public string? BrandName { get; set; }
    public string CategorySlug { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public List<ProductAttributeDto> Attributes { get; set; } = new();
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
}

public class CatalogQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 24;
    public string? Sort { get; set; }
    public List<string> Brands { get; set; } = new();
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public Dictionary<string, List<string>> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
