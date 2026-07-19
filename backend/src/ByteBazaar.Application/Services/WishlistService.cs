using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using ByteBazaar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

public class WishlistService
{
    private readonly IAppDbContext _db;

    public WishlistService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProductListItemDto>> GetWishlistAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.WishlistItems.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new ProductListItemDto
            {
                Id = w.Product!.Id,
                Name = w.Product.Name,
                Slug = w.Product.Slug,
                Price = w.Product.Price,
                SalePrice = w.Product.SalePrice,
                ImageUrl = w.Product.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                BrandName = w.Product.Brand != null ? w.Product.Brand.Name : null,
                Stock = w.Product.Stock
            })
            .ToListAsync(ct);
    }

    public async Task AddAsync(Guid userId, Guid productId, CancellationToken ct = default)
    {
        var exists = await _db.Products.AnyAsync(p => p.Id == productId, ct);
        if (!exists) throw new NotFoundException("Product not found.");

        var already = await _db.WishlistItems.AnyAsync(w => w.UserId == userId && w.ProductId == productId, ct);
        if (already) return;

        _db.WishlistItems.Add(new WishlistItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductId = productId,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid userId, Guid productId, CancellationToken ct = default)
    {
        var item = await _db.WishlistItems
            .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId, ct);
        if (item is null) return;
        _db.WishlistItems.Remove(item);
        await _db.SaveChangesAsync(ct);
    }
}
