using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;

namespace ByteBazaar.Application.Services;

/// <summary>
/// Coupon validation + discount math shared by CartService (recompute on every cart read),
/// CouponService (apply) and CheckoutService (re-validation before order creation).
/// </summary>
public static class CouponRules
{
    public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Returns a human-readable rejection reason, or null when the coupon is usable.</summary>
    public static string? GetRejectionReason(Coupon coupon, decimal subtotal, DateTime now)
    {
        if (!coupon.IsActive)
            return $"Coupon \"{coupon.Code}\" is not active.";
        if (coupon.ValidFrom is not null && coupon.ValidFrom > now)
            return $"Coupon \"{coupon.Code}\" is not valid yet.";
        if (coupon.ValidTo is not null && coupon.ValidTo < now)
            return $"Coupon \"{coupon.Code}\" has expired.";
        if (coupon.MinOrderAmount is not null && subtotal < coupon.MinOrderAmount.Value)
            return $"Coupon \"{coupon.Code}\" requires a minimum order of {coupon.MinOrderAmount.Value:0.##}.";
        if (coupon.MaxUses is not null && coupon.UsedCount >= coupon.MaxUses.Value)
            return $"Coupon \"{coupon.Code}\" has reached its usage limit.";
        return null;
    }

    /// <summary>Percent: subtotal * value / 100 rounded to 2dp; Fixed: min(value, subtotal).</summary>
    public static decimal ComputeDiscount(Coupon coupon, decimal subtotal) => coupon.Type switch
    {
        CouponType.Percent => Math.Round(subtotal * coupon.Value / 100m, 2, MidpointRounding.AwayFromZero),
        _ => Math.Min(coupon.Value, subtotal)
    };
}
