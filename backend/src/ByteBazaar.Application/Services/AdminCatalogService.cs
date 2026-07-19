using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

public class AdminCatalogService
{
    private readonly IAppDbContext _db;

    public AdminCatalogService(IAppDbContext db)
    {
        _db = db;
    }

    // ----- Categories -----

    public async Task<List<AdminCategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return await _db.Categories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new AdminCategoryDto
            {
                Id = c.Id,
                ParentId = c.ParentId,
                Name = c.Name,
                Slug = c.Slug,
                ImageUrl = c.ImageUrl,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive,
                MetaTitle = c.MetaTitle,
                MetaDescription = c.MetaDescription
            })
            .ToListAsync(ct);
    }

    public async Task<AdminCategoryDto> CreateCategoryAsync(CategoryUpsertRequest request, CancellationToken ct = default)
    {
        var category = new Category { Id = Guid.NewGuid() };
        Apply(category, request);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<AdminCategoryDto?> UpdateCategoryAsync(Guid id, CategoryUpsertRequest request, CancellationToken ct = default)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return null;
        Apply(category, request);
        await _db.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<bool> DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return false;
        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void Apply(Category category, CategoryUpsertRequest request)
    {
        category.Name = request.Name;
        category.Slug = request.Slug;
        category.ParentId = request.ParentId;
        category.ImageUrl = request.ImageUrl;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        category.MetaTitle = request.MetaTitle;
        category.MetaDescription = request.MetaDescription;
    }

    private static AdminCategoryDto ToDto(Category c) => new()
    {
        Id = c.Id,
        ParentId = c.ParentId,
        Name = c.Name,
        Slug = c.Slug,
        ImageUrl = c.ImageUrl,
        SortOrder = c.SortOrder,
        IsActive = c.IsActive,
        MetaTitle = c.MetaTitle,
        MetaDescription = c.MetaDescription
    };

    // ----- Attribute definitions -----

    public async Task<List<AdminAttributeDto>?> GetCategoryAttributesAsync(Guid categoryId, CancellationToken ct = default)
    {
        var exists = await _db.Categories.AnyAsync(c => c.Id == categoryId, ct);
        if (!exists) return null;

        var attributes = await _db.AttributeDefinitions.AsNoTracking()
            .Where(a => a.CategoryId == categoryId)
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Name)
            .ToListAsync(ct);
        return attributes.Select(ToDto).ToList();
    }

    public async Task<AdminAttributeDto?> CreateAttributeAsync(AttributeUpsertRequest request, CancellationToken ct = default)
    {
        var exists = await _db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct);
        if (!exists) return null;

        var attribute = new AttributeDefinition { Id = Guid.NewGuid() };
        Apply(attribute, request);
        _db.AttributeDefinitions.Add(attribute);
        await _db.SaveChangesAsync(ct);
        return ToDto(attribute);
    }

    public async Task<AdminAttributeDto?> UpdateAttributeAsync(Guid id, AttributeUpsertRequest request, CancellationToken ct = default)
    {
        var attribute = await _db.AttributeDefinitions.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (attribute is null) return null;
        Apply(attribute, request);
        await _db.SaveChangesAsync(ct);
        return ToDto(attribute);
    }

    public async Task<bool> DeleteAttributeAsync(Guid id, CancellationToken ct = default)
    {
        var attribute = await _db.AttributeDefinitions.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (attribute is null) return false;
        _db.AttributeDefinitions.Remove(attribute);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void Apply(AttributeDefinition attribute, AttributeUpsertRequest request)
    {
        attribute.CategoryId = request.CategoryId;
        attribute.Name = request.Name;
        attribute.Code = request.Code;
        attribute.Type = request.Type;
        attribute.Options = request.Options ?? new List<string>();
        attribute.IsFilterable = request.IsFilterable;
        attribute.IsRequired = request.IsRequired;
        attribute.FilterWidget = request.FilterWidget;
        attribute.SortOrder = request.SortOrder;
    }

    private static AdminAttributeDto ToDto(AttributeDefinition a) => new()
    {
        Id = a.Id,
        CategoryId = a.CategoryId,
        Name = a.Name,
        Code = a.Code,
        Type = a.Type,
        Options = a.Options,
        IsFilterable = a.IsFilterable,
        IsRequired = a.IsRequired,
        FilterWidget = a.FilterWidget,
        SortOrder = a.SortOrder
    };

    // ----- Brands -----

    public async Task<List<AdminBrandDto>> GetBrandsAsync(CancellationToken ct = default)
    {
        return await _db.Brands.AsNoTracking()
            .OrderBy(b => b.Name)
            .Select(b => new AdminBrandDto { Id = b.Id, Name = b.Name, Slug = b.Slug, LogoUrl = b.LogoUrl })
            .ToListAsync(ct);
    }

    public async Task<AdminBrandDto> CreateBrandAsync(BrandUpsertRequest request, CancellationToken ct = default)
    {
        var brand = new Brand { Id = Guid.NewGuid(), Name = request.Name, Slug = request.Slug, LogoUrl = request.LogoUrl };
        _db.Brands.Add(brand);
        await _db.SaveChangesAsync(ct);
        return new AdminBrandDto { Id = brand.Id, Name = brand.Name, Slug = brand.Slug, LogoUrl = brand.LogoUrl };
    }

    public async Task<AdminBrandDto?> UpdateBrandAsync(Guid id, BrandUpsertRequest request, CancellationToken ct = default)
    {
        var brand = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (brand is null) return null;
        brand.Name = request.Name;
        brand.Slug = request.Slug;
        brand.LogoUrl = request.LogoUrl;
        await _db.SaveChangesAsync(ct);
        return new AdminBrandDto { Id = brand.Id, Name = brand.Name, Slug = brand.Slug, LogoUrl = brand.LogoUrl };
    }

    public async Task<bool> DeleteBrandAsync(Guid id, CancellationToken ct = default)
    {
        var brand = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (brand is null) return false;
        _db.Brands.Remove(brand);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----- Products -----

    public async Task<PagedResultDto<AdminProductListItemDto>> GetProductsAsync(
        int page, int pageSize, string? search, Guid? categoryId, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);

        var products = _db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            products = products.Where(p => p.Name.ToLower().Contains(term) || p.Slug.ToLower().Contains(term));
        }
        if (categoryId is not null)
            products = products.Where(p => p.CategoryId == categoryId.Value);

        var totalCount = await products.CountAsync(ct);
        var items = await products
            .OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new AdminProductListItemDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : null,
                BrandId = p.BrandId,
                BrandName = p.Brand != null ? p.Brand.Name : null,
                Price = p.Price,
                SalePrice = p.SalePrice,
                Stock = p.Stock,
                Status = p.Status,
                ImageUrl = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResultDto<AdminProductListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminProductDetailDto?> GetProductAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _db.Products.AsNoTracking()
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        return product is null ? null : ToDto(product);
    }

    public async Task<AdminProductDetailDto> CreateProductAsync(ProductUpsertRequest request, CancellationToken ct = default)
    {
        var product = new Product { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        Apply(product, request);
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        return ToDto(product);
    }

    public async Task<AdminProductDetailDto?> UpdateProductAsync(Guid id, ProductUpsertRequest request, CancellationToken ct = default)
    {
        var product = await _db.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return null;

        _db.ProductImages.RemoveRange(product.Images);
        product.Images.Clear();
        Apply(product, request);
        await _db.SaveChangesAsync(ct);
        return ToDto(product);
    }

    public async Task<bool> DeleteProductAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return false;
        _db.Products.Remove(product);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void Apply(Product product, ProductUpsertRequest request)
    {
        product.Name = request.Name;
        product.Slug = request.Slug;
        product.CategoryId = request.CategoryId;
        product.BrandId = request.BrandId;
        product.Description = request.Description;
        product.Price = request.Price;
        product.SalePrice = request.SalePrice;
        product.Stock = request.Stock;
        product.Status = request.Status;
        product.Attributes = request.Attributes is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(request.Attributes);
        product.MetaTitle = request.MetaTitle;
        product.MetaDescription = request.MetaDescription;

        var sortOrder = 0;
        foreach (var url in request.Images ?? new List<string>())
        {
            product.Images.Add(new ProductImage
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Url = url,
                SortOrder = sortOrder++
            });
        }
    }

    private static AdminProductDetailDto ToDto(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Slug = p.Slug,
        CategoryId = p.CategoryId,
        BrandId = p.BrandId,
        Description = p.Description,
        Price = p.Price,
        SalePrice = p.SalePrice,
        Stock = p.Stock,
        Status = p.Status,
        Images = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
        Attributes = new Dictionary<string, string>(p.Attributes),
        MetaTitle = p.MetaTitle,
        MetaDescription = p.MetaDescription,
        CreatedAt = p.CreatedAt
    };
}
