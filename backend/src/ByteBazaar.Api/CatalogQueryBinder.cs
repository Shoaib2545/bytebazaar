using ByteBazaar.Application.DTOs;

namespace ByteBazaar.Api;

/// <summary>
/// Turns the storefront's URL search params into a <see cref="CatalogQuery"/>. Reserved keys are
/// paging/sorting/brand/price/q; every other key is treated as a dynamic attribute code, with
/// comma-separated values OR'd inside the attribute and attributes AND'd across.
/// Shared by the catalog and search endpoints so both parse filters identically.
/// </summary>
public static class CatalogQueryBinder
{
    public static readonly string[] ReservedQueryKeys = { "page", "pagesize", "sort", "brand", "price", "q" };

    public static CatalogQuery FromRequest(HttpRequest request)
    {
        var query = new CatalogQuery();

        if (int.TryParse(request.Query["page"], out var page)) query.Page = page;
        if (int.TryParse(request.Query["pageSize"], out var pageSize)) query.PageSize = pageSize;

        var sort = request.Query["sort"].ToString();
        if (!string.IsNullOrWhiteSpace(sort)) query.Sort = sort.Trim().ToLowerInvariant();

        var brand = request.Query["brand"].ToString();
        if (!string.IsNullOrWhiteSpace(brand))
            query.Brands = SplitValues(brand);

        var price = request.Query["price"].ToString();
        if (!string.IsNullOrWhiteSpace(price))
        {
            var parts = price.Split('-', 2);
            if (parts.Length == 2)
            {
                if (decimal.TryParse(parts[0], out var min)) query.PriceMin = min;
                if (decimal.TryParse(parts[1], out var max)) query.PriceMax = max;
            }
        }

        foreach (var (key, values) in request.Query)
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
