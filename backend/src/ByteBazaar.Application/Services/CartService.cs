using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

public class CartService
{
    private readonly IAppDbContext _db;

    public CartService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<CartDto> GetCartAsync(Guid? userId, Guid? anonymousId, CancellationToken ct = default)
    {
        var cart = await FindCartAsync(userId, anonymousId, track: false, ct);
        return await BuildDtoAsync(cart, ct);
    }

    public async Task<CartDto> AddItemAsync(Guid? userId, Guid? anonymousId, AddCartItemRequest request, CancellationToken ct = default)
    {
        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.Status == ProductStatus.Active, ct)
            ?? throw new NotFoundException("Product not found.");

        var cart = await GetOrCreateCartAsync(userId, anonymousId, ct);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
        var newQuantity = (item?.Quantity ?? 0) + request.Quantity;

        if (newQuantity > product.Stock)
            throw new BadRequestException($"Only {product.Stock} unit(s) of \"{product.Name}\" in stock.");

        if (item is null)
        {
            item = new CartItem { Id = Guid.NewGuid(), CartId = cart.Id, ProductId = request.ProductId, Quantity = newQuantity };
            _db.CartItems.Add(item); // change-tracker fixup also appends it to cart.Items
        }
        else
        {
            item.Quantity = newQuantity;
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(cart, ct);
    }

    public async Task<CartDto> UpdateItemAsync(Guid? userId, Guid? anonymousId, Guid productId, int quantity, CancellationToken ct = default)
    {
        var cart = await FindCartAsync(userId, anonymousId, track: true, ct)
            ?? throw new NotFoundException("Cart is empty.");
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new NotFoundException("Item is not in the cart.");

        if (quantity <= 0)
        {
            _db.CartItems.Remove(item);
            cart.Items.Remove(item);
        }
        else
        {
            var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct)
                ?? throw new NotFoundException("Product not found.");
            if (quantity > product.Stock)
                throw new BadRequestException($"Only {product.Stock} unit(s) of \"{product.Name}\" in stock.");
            item.Quantity = quantity;
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(cart, ct);
    }

    public async Task<CartDto> RemoveItemAsync(Guid? userId, Guid? anonymousId, Guid productId, CancellationToken ct = default)
    {
        var cart = await FindCartAsync(userId, anonymousId, track: true, ct);
        var item = cart?.Items.FirstOrDefault(i => i.ProductId == productId);
        if (cart is not null && item is not null)
        {
            _db.CartItems.Remove(item);
            cart.Items.Remove(item);
            cart.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return await BuildDtoAsync(cart, ct);
    }

    /// <summary>
    /// Merges the anonymous cookie cart into the authenticated user's cart,
    /// summing quantities capped at available stock, then deletes the anonymous cart.
    /// </summary>
    public async Task<CartDto> MergeAsync(Guid userId, Guid? anonymousId, CancellationToken ct = default)
    {
        var userCart = await GetOrCreateCartAsync(userId, null, ct);

        if (anonymousId is not null)
        {
            var anonCart = await _db.Carts.Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.AnonymousId == anonymousId && c.UserId == null, ct);

            if (anonCart is not null && anonCart.Id != userCart.Id)
            {
                var productIds = anonCart.Items.Select(i => i.ProductId).ToList();
                var stocks = await _db.Products.AsNoTracking()
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Stock, ct);

                foreach (var anonItem in anonCart.Items)
                {
                    if (!stocks.TryGetValue(anonItem.ProductId, out var stock)) continue;

                    var existing = userCart.Items.FirstOrDefault(i => i.ProductId == anonItem.ProductId);
                    var merged = Math.Min((existing?.Quantity ?? 0) + anonItem.Quantity, stock);
                    if (merged <= 0) continue;

                    if (existing is null)
                    {
                        _db.CartItems.Add(new CartItem
                        {
                            Id = Guid.NewGuid(),
                            CartId = userCart.Id,
                            ProductId = anonItem.ProductId,
                            Quantity = merged
                        }); // fixup appends it to userCart.Items
                    }
                    else
                    {
                        existing.Quantity = merged;
                    }
                }

                _db.CartItems.RemoveRange(anonCart.Items);
                _db.Carts.Remove(anonCart);
                userCart.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
        }

        return await BuildDtoAsync(userCart, ct);
    }

    internal async Task<Cart?> FindCartAsync(Guid? userId, Guid? anonymousId, bool track, CancellationToken ct)
    {
        IQueryable<Cart> carts = _db.Carts.Include(c => c.Items);
        if (!track) carts = carts.AsNoTracking();

        if (userId is not null)
            return await carts.FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (anonymousId is not null)
            return await carts.FirstOrDefaultAsync(c => c.AnonymousId == anonymousId && c.UserId == null, ct);
        return null;
    }

    internal async Task<Cart> GetOrCreateCartAsync(Guid? userId, Guid? anonymousId, CancellationToken ct)
    {
        var cart = await FindCartAsync(userId, anonymousId, track: true, ct);
        if (cart is not null) return cart;

        if (userId is null && anonymousId is null)
            throw new BadRequestException("No cart identity available.");

        cart = new Cart
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AnonymousId = userId is null ? anonymousId : null,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Carts.Add(cart);
        await _db.SaveChangesAsync(ct);
        return cart;
    }

    internal async Task<CartDto> BuildDtoAsync(Cart? cart, CancellationToken ct)
    {
        var dto = new CartDto();
        if (cart is null || cart.Items.Count == 0) return dto;

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.Price,
                p.SalePrice,
                p.Stock,
                ImageUrl = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()
            })
            .ToListAsync(ct);

        foreach (var item in cart.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product is null) continue;

            var unitPrice = product.SalePrice ?? product.Price;
            dto.Items.Add(new CartItemDto
            {
                ProductId = product.Id,
                Name = product.Name,
                Slug = product.Slug,
                ImageUrl = product.ImageUrl,
                UnitPrice = unitPrice,
                Quantity = item.Quantity,
                LineTotal = unitPrice * item.Quantity,
                Stock = product.Stock
            });
        }

        dto.Subtotal = dto.Items.Sum(i => i.LineTotal);
        dto.ItemCount = dto.Items.Sum(i => i.Quantity);
        return dto;
    }
}
