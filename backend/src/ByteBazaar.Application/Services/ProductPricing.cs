using System.Linq.Expressions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Domain.Entities;

namespace ByteBazaar.Application.Services;

/// <summary>
/// Single source of truth for the effective-price rule: a product's SalePrice applies ONLY
/// while <c>now</c> is within [SaleStart, SaleEnd] (a null bound is unbounded on that side).
/// The expression helpers translate on both Npgsql and the InMemory provider; the plain
/// helpers are for already-materialized values (cart/checkout/detail projections).
/// </summary>
public static class ProductPricing
{
    public static bool IsSaleActive(decimal? salePrice, DateTime? saleStart, DateTime? saleEnd, DateTime now)
        => salePrice is not null
           && (saleStart is null || saleStart <= now)
           && (saleEnd is null || saleEnd >= now);

    /// <summary>SalePrice when its window is active, otherwise null (for public DTOs).</summary>
    public static decimal? EffectiveSalePrice(decimal? salePrice, DateTime? saleStart, DateTime? saleEnd, DateTime now)
        => IsSaleActive(salePrice, saleStart, saleEnd, now) ? salePrice : null;

    /// <summary>The price a customer actually pays right now.</summary>
    public static decimal EffectiveUnitPrice(decimal price, decimal? salePrice, DateTime? saleStart, DateTime? saleEnd, DateTime now)
        => EffectiveSalePrice(salePrice, saleStart, saleEnd, now) ?? price;

    /// <summary>Effective price as a translatable ordering/selector expression.</summary>
    public static Expression<Func<Product, decimal>> EffectivePrice(DateTime now) =>
        p => p.SalePrice != null
             && (p.SaleStart == null || p.SaleStart <= now)
             && (p.SaleEnd == null || p.SaleEnd >= now)
            ? p.SalePrice.Value
            : p.Price;

    public static Expression<Func<Product, bool>> EffectivePriceAtLeast(decimal min, DateTime now) =>
        p => (p.SalePrice != null
              && (p.SaleStart == null || p.SaleStart <= now)
              && (p.SaleEnd == null || p.SaleEnd >= now)
            ? p.SalePrice.Value
            : p.Price) >= min;

    public static Expression<Func<Product, bool>> EffectivePriceAtMost(decimal max, DateTime now) =>
        p => (p.SalePrice != null
              && (p.SaleStart == null || p.SaleStart <= now)
              && (p.SaleEnd == null || p.SaleEnd >= now)
            ? p.SalePrice.Value
            : p.Price) <= max;

    /// <summary>Shared product-card projection; SalePrice is exposed only while the window is active.</summary>
    public static Expression<Func<Product, ProductListItemDto>> ToProductCard(DateTime now) =>
        p => new ProductListItemDto
        {
            Id = p.Id,
            Name = p.Name,
            Slug = p.Slug,
            Price = p.Price,
            SalePrice = p.SalePrice != null
                        && (p.SaleStart == null || p.SaleStart <= now)
                        && (p.SaleEnd == null || p.SaleEnd >= now)
                ? p.SalePrice
                : null,
            ImageUrl = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
            BrandName = p.Brand != null ? p.Brand.Name : null,
            Stock = p.Stock
        };
}
