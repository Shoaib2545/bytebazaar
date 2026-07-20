using ByteBazaar.Application.Abstractions;
using Microsoft.AspNetCore.OutputCaching;

namespace ByteBazaar.Api;

/// <summary>
/// ASP.NET Core output-cache policies for the heavy public catalog endpoints. All of them are
/// tagged <see cref="CacheTags.Catalog"/> so a single EvictByTag on an admin catalog write clears
/// every cached filter combination at once (see AdminCatalogService.InvalidateCatalogCachesAsync).
/// </summary>
public static class CachePolicies
{
    public const string CategoryTree = "catalog-tree";
    public const string CatalogFilters = "catalog-filters";
    public const string CatalogProducts = "catalog-products";

    public static void Configure(OutputCacheOptions options)
    {
        // The default cache key already includes path + full query string, which is exactly the
        // granularity we want: /categories/laptops/products?brand=asus&ram=16GB&page=2 is its own
        // entry. Anonymous only — SetVaryByHeader(Authorization) would let a signed-in admin's
        // response be served to the public, so authenticated requests bypass the cache entirely.
        options.AddPolicy(CategoryTree, builder => builder
            .Tag(CacheTags.Catalog)
            .Expire(TimeSpan.FromMinutes(10))
            .With(context => context.HttpContext.User.Identity?.IsAuthenticated != true));

        options.AddPolicy(CatalogFilters, builder => builder
            .Tag(CacheTags.Catalog)
            .SetVaryByQuery("*")
            .Expire(TimeSpan.FromMinutes(2))
            .With(context => context.HttpContext.User.Identity?.IsAuthenticated != true));

        options.AddPolicy(CatalogProducts, builder => builder
            .Tag(CacheTags.Catalog)
            .SetVaryByQuery("*")
            .Expire(TimeSpan.FromSeconds(60))
            .With(context => context.HttpContext.User.Identity?.IsAuthenticated != true));
    }
}
