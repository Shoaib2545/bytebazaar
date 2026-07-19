using System.Linq.Expressions;
using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

public class CatalogService
{
    private readonly IAppDbContext _db;

    public CatalogService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<List<CategoryTreeDto>> GetCategoryTreeAsync(CancellationToken ct = default)
    {
        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync(ct);

        List<CategoryTreeDto> Build(Guid? parentId) =>
            categories.Where(c => c.ParentId == parentId)
                .Select(c => new CategoryTreeDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Slug = c.Slug,
                    ImageUrl = c.ImageUrl,
                    SortOrder = c.SortOrder,
                    Children = Build(c.Id)
                })
                .ToList();

        return Build(null);
    }

    public async Task<CategoryFiltersDto?> GetCategoryFiltersAsync(string slug, CancellationToken ct = default)
    {
        var categories = await _db.Categories.AsNoTracking().ToListAsync(ct);
        var category = categories.FirstOrDefault(c => c.Slug == slug && c.IsActive);
        if (category is null) return null;

        var subtreeIds = GetSubtreeIds(categories, category.Id);
        var lineageIds = GetLineageIds(categories, category.Id);

        var definitions = await _db.AttributeDefinitions.AsNoTracking()
            .Where(a => lineageIds.Contains(a.CategoryId) && a.IsFilterable)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Name)
            .ToListAsync(ct);

        // Materialize the lightweight product projection once; counts are computed in memory.
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active && subtreeIds.Contains(p.CategoryId))
            .Select(p => new { p.BrandId, p.Price, p.SalePrice, p.Attributes })
            .ToListAsync(ct);

        var result = new CategoryFiltersDto();

        foreach (var def in definitions)
        {
            var dto = new AttributeFilterDto
            {
                Code = def.Code,
                Name = def.Name,
                Type = def.Type.ToString(),
                Widget = def.FilterWidget.ToString()
            };

            var valueCounts = products
                .Where(p => p.Attributes.ContainsKey(def.Code))
                .GroupBy(p => p.Attributes[def.Code])
                .ToDictionary(g => g.Key, g => g.Count());

            var optionValues = def.Options.Count > 0
                ? def.Options
                : valueCounts.Keys.OrderBy(v => v).ToList();

            foreach (var value in optionValues)
            {
                dto.Options.Add(new FilterOptionDto
                {
                    Value = value,
                    Count = valueCounts.TryGetValue(value, out var count) ? count : 0
                });
            }

            result.Attributes.Add(dto);
        }

        var brandCounts = products
            .Where(p => p.BrandId != null)
            .GroupBy(p => p.BrandId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var brands = await _db.Brands.AsNoTracking()
            .Where(b => brandCounts.Keys.Contains(b.Id))
            .OrderBy(b => b.Name)
            .ToListAsync(ct);

        result.Brands = brands.Select(b => new BrandFilterDto
        {
            Id = b.Id,
            Name = b.Name,
            Slug = b.Slug,
            Count = brandCounts[b.Id]
        }).ToList();

        if (products.Count > 0)
        {
            var effectivePrices = products.Select(p => p.SalePrice ?? p.Price).ToList();
            result.PriceRange = new PriceRangeDto { Min = effectivePrices.Min(), Max = effectivePrices.Max() };
        }

        return result;
    }

    public async Task<PagedResultDto<ProductListItemDto>?> GetCategoryProductsAsync(
        string slug, CatalogQuery query, CancellationToken ct = default)
    {
        var categories = await _db.Categories.AsNoTracking().ToListAsync(ct);
        var category = categories.FirstOrDefault(c => c.Slug == slug && c.IsActive);
        if (category is null) return null;

        var subtreeIds = GetSubtreeIds(categories, category.Id);
        var lineageIds = GetLineageIds(categories, category.Id);

        var filterableCodes = await _db.AttributeDefinitions.AsNoTracking()
            .Where(a => lineageIds.Contains(a.CategoryId) && a.IsFilterable)
            .Select(a => a.Code)
            .ToListAsync(ct);

        var products = _db.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active && subtreeIds.Contains(p.CategoryId));

        products = await ApplyFiltersAsync(products, query, filterableCodes, ct);
        return await PageAsync(products, query, ct);
    }

    public async Task<ProductDetailDto?> GetProductAsync(string slug, CancellationToken ct = default)
    {
        var product = await _db.Products.AsNoTracking()
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.Status == ProductStatus.Active, ct);
        if (product is null) return null;

        var codes = product.Attributes.Keys.ToList();
        var definitions = await _db.AttributeDefinitions.AsNoTracking()
            .Where(a => codes.Contains(a.Code))
            .ToListAsync(ct);

        var attributes = product.Attributes
            .Select(kv =>
            {
                var def = definitions.FirstOrDefault(d => d.Code == kv.Key);
                return new { Name = def?.Name ?? kv.Key, Value = kv.Value, SortOrder = def?.SortOrder ?? int.MaxValue };
            })
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Name)
            .Select(a => new ProductAttributeDto { Name = a.Name, Value = a.Value })
            .ToList();

        return new ProductDetailDto
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            Description = product.Description,
            Price = product.Price,
            SalePrice = product.SalePrice,
            Stock = product.Stock,
            BrandName = product.Brand?.Name,
            CategorySlug = product.Category?.Slug ?? string.Empty,
            CategoryName = product.Category?.Name ?? string.Empty,
            Images = product.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
            Attributes = attributes,
            MetaTitle = product.MetaTitle,
            MetaDescription = product.MetaDescription
        };
    }

    public async Task<PagedResultDto<ProductListItemDto>> SearchAsync(
        string? q, CatalogQuery query, CancellationToken ct = default)
    {
        var products = _db.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            products = products.Where(p =>
                p.Name.ToLower().Contains(term) ||
                (p.Description != null && p.Description.ToLower().Contains(term)));
        }

        products = await ApplyFiltersAsync(products, query, filterableCodes: null, ct);
        return await PageAsync(products, query, ct);
    }

    private async Task<IQueryable<Product>> ApplyFiltersAsync(
        IQueryable<Product> products, CatalogQuery query, List<string>? filterableCodes, CancellationToken ct)
    {
        if (query.Brands.Count > 0)
        {
            var brandIds = await _db.Brands.AsNoTracking()
                .Where(b => query.Brands.Contains(b.Slug))
                .Select(b => b.Id)
                .ToListAsync(ct);
            products = products.Where(p => p.BrandId != null && brandIds.Contains(p.BrandId.Value));
        }

        if (query.PriceMin is not null)
            products = products.Where(p => (p.SalePrice ?? p.Price) >= query.PriceMin.Value);
        if (query.PriceMax is not null)
            products = products.Where(p => (p.SalePrice ?? p.Price) <= query.PriceMax.Value);

        foreach (var (code, values) in query.Attributes)
        {
            if (values.Count == 0) continue;
            if (filterableCodes is not null &&
                !filterableCodes.Contains(code, StringComparer.OrdinalIgnoreCase)) continue;

            var actualCode = filterableCodes?.First(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase)) ?? code;
            products = products.Where(_db.BuildAttributeFilter(actualCode, values));
        }

        return products;
    }

    private static async Task<PagedResultDto<ProductListItemDto>> PageAsync(
        IQueryable<Product> products, CatalogQuery query, CancellationToken ct)
    {
        products = query.Sort switch
        {
            "price_asc" => products.OrderBy(p => p.SalePrice ?? p.Price).ThenBy(p => p.Id),
            "price_desc" => products.OrderByDescending(p => p.SalePrice ?? p.Price).ThenBy(p => p.Id),
            _ => products.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
        };

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var totalCount = await products.CountAsync(ct);
        var items = await products
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItemDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Price = p.Price,
                SalePrice = p.SalePrice,
                ImageUrl = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                BrandName = p.Brand != null ? p.Brand.Name : null,
                Stock = p.Stock
            })
            .ToListAsync(ct);

        return new PagedResultDto<ProductListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static List<Guid> GetSubtreeIds(List<Category> all, Guid rootId)
    {
        var result = new List<Guid> { rootId };
        var queue = new Queue<Guid>();
        queue.Enqueue(rootId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in all.Where(c => c.ParentId == current))
            {
                result.Add(child.Id);
                queue.Enqueue(child.Id);
            }
        }
        return result;
    }

    private static List<Guid> GetLineageIds(List<Category> all, Guid categoryId)
    {
        var result = new List<Guid>();
        Guid? current = categoryId;
        while (current is not null)
        {
            result.Add(current.Value);
            current = all.FirstOrDefault(c => c.Id == current.Value)?.ParentId;
        }
        return result;
    }
}
