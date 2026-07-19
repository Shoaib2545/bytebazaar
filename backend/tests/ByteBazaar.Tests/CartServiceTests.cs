using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using ByteBazaar.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Tests;

public class CartServiceTests
{
    private static readonly Guid AnonId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task AddItem_CreatesCartAndIncrementsQuantity()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CartService(db);
        var product = await db.Products.SingleAsync(p => p.Slug == "laptop-a");

        var cart = await service.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = product.Id, Quantity = 2 });
        Assert.Single(cart.Items);
        Assert.Equal(2, cart.ItemCount);

        cart = await service.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = product.Id, Quantity = 3 });
        Assert.Single(cart.Items);
        Assert.Equal(5, cart.Items[0].Quantity);
        Assert.Equal(100000m * 5, cart.Subtotal);
    }

    [Fact]
    public async Task AddItem_UsesEffectiveSalePrice()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CartService(db);
        var onSale = await db.Products.SingleAsync(p => p.Slug == "laptop-b"); // 200000, sale 180000

        var cart = await service.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = onSale.Id, Quantity = 2 });

        Assert.Equal(180000m, cart.Items[0].UnitPrice);
        Assert.Equal(360000m, cart.Items[0].LineTotal);
        Assert.Equal(360000m, cart.Subtotal);
    }

    [Fact]
    public async Task AddItem_RejectsQuantityAboveStock()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CartService(db);
        var product = await db.Products.SingleAsync(p => p.Slug == "laptop-a"); // stock 5

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = product.Id, Quantity = 6 }));

        // Incrementing beyond stock must also be rejected.
        await service.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = product.Id, Quantity = 3 });
        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = product.Id, Quantity = 3 }));
    }

    [Fact]
    public async Task AddItem_UnknownProduct_ThrowsNotFound()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CartService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = Guid.NewGuid(), Quantity = 1 }));
    }

    [Fact]
    public async Task UpdateItem_SetsQuantity_ZeroRemoves_AndOverStockRejected()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CartService(db);
        var product = await db.Products.SingleAsync(p => p.Slug == "laptop-a"); // stock 5

        await service.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = product.Id, Quantity = 1 });

        var cart = await service.UpdateItemAsync(null, AnonId, product.Id, 4);
        Assert.Equal(4, cart.Items[0].Quantity);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.UpdateItemAsync(null, AnonId, product.Id, 6));

        cart = await service.UpdateItemAsync(null, AnonId, product.Id, 0);
        Assert.Empty(cart.Items);
        Assert.Equal(0, cart.ItemCount);
    }

    [Fact]
    public async Task RemoveItem_RemovesLine()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CartService(db);
        var a = await db.Products.SingleAsync(p => p.Slug == "laptop-a");
        var b = await db.Products.SingleAsync(p => p.Slug == "laptop-b");

        await service.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = a.Id, Quantity = 1 });
        await service.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = b.Id, Quantity = 1 });

        var cart = await service.RemoveItemAsync(null, AnonId, a.Id);
        Assert.Single(cart.Items);
        Assert.Equal(b.Id, cart.Items[0].ProductId);
    }

    [Fact]
    public async Task Merge_SumsQuantities_CappedAtStock_AndDeletesAnonymousCart()
    {
        await using var db = await TestDbFactory.CreateSeededAsync();
        var service = new CartService(db);
        var a = await db.Products.SingleAsync(p => p.Slug == "laptop-a"); // stock 5
        var b = await db.Products.SingleAsync(p => p.Slug == "laptop-b");

        // Anonymous cart: 4x laptop-a, 1x laptop-b.
        await service.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = a.Id, Quantity = 4 });
        await service.AddItemAsync(null, AnonId, new AddCartItemRequest { ProductId = b.Id, Quantity = 1 });

        // User cart: 3x laptop-a.
        await service.AddItemAsync(UserId, null, new AddCartItemRequest { ProductId = a.Id, Quantity = 3 });

        var merged = await service.MergeAsync(UserId, AnonId);

        Assert.Equal(2, merged.Items.Count);
        Assert.Equal(5, merged.Items.Single(i => i.ProductId == a.Id).Quantity); // 4 + 3 capped at stock 5
        Assert.Equal(1, merged.Items.Single(i => i.ProductId == b.Id).Quantity);
        Assert.False(await db.Carts.AnyAsync(c => c.AnonymousId == AnonId));

        // Merging again with no anonymous cart is a no-op.
        var again = await service.MergeAsync(UserId, AnonId);
        Assert.Equal(2, again.Items.Count);
    }
}
