using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

/// <summary>Applies/removes a coupon on the current cart (public endpoints).</summary>
public class CouponService
{
    private readonly IAppDbContext _db;
    private readonly CartService _cartService;

    public CouponService(IAppDbContext db, CartService cartService)
    {
        _db = db;
        _cartService = cartService;
    }

    public async Task<CartDto> ApplyAsync(Guid? userId, Guid? anonymousId, string code, CancellationToken ct = default)
    {
        var normalized = CouponRules.NormalizeCode(code);
        if (normalized.Length == 0)
            throw new BadRequestException("Coupon code is required.");

        var coupon = await _db.Coupons.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == normalized, ct)
            ?? throw new BadRequestException($"Unknown coupon code \"{normalized}\".");

        var cart = await _cartService.FindCartAsync(userId, anonymousId, track: true, ct);
        if (cart is null || cart.Items.Count == 0)
            throw new BadRequestException("Your cart is empty.");

        // Subtotal uses effective prices; ignore any previously applied coupon.
        var subtotal = (await _cartService.BuildDtoAsync(cart, ct)).Subtotal;

        var reason = CouponRules.GetRejectionReason(coupon, subtotal, DateTime.UtcNow);
        if (reason is not null)
            throw new BadRequestException(reason);

        cart.CouponCode = coupon.Code;
        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await _cartService.BuildDtoAsync(cart, ct);
    }

    public async Task<CartDto> RemoveAsync(Guid? userId, Guid? anonymousId, CancellationToken ct = default)
    {
        var cart = await _cartService.FindCartAsync(userId, anonymousId, track: true, ct);
        if (cart?.CouponCode is not null)
        {
            cart.CouponCode = null;
            cart.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return await _cartService.BuildDtoAsync(cart, ct);
    }
}
