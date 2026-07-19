using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using ByteBazaar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

public class AdminCouponService
{
    private readonly IAppDbContext _db;

    public AdminCouponService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<List<AdminCouponDto>> GetCouponsAsync(CancellationToken ct = default)
    {
        var coupons = await _db.Coupons.AsNoTracking()
            .OrderBy(c => c.Code)
            .ToListAsync(ct);
        return coupons.Select(ToDto).ToList();
    }

    public async Task<AdminCouponDto> CreateAsync(CouponUpsertRequest request, CancellationToken ct = default)
    {
        var code = CouponRules.NormalizeCode(request.Code);
        if (await _db.Coupons.AnyAsync(c => c.Code == code, ct))
            throw new BadRequestException($"A coupon with code \"{code}\" already exists.");

        var coupon = new Coupon { Id = Guid.NewGuid() };
        Apply(coupon, request);
        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync(ct);
        return ToDto(coupon);
    }

    public async Task<AdminCouponDto?> UpdateAsync(Guid id, CouponUpsertRequest request, CancellationToken ct = default)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (coupon is null) return null;

        var code = CouponRules.NormalizeCode(request.Code);
        if (await _db.Coupons.AnyAsync(c => c.Code == code && c.Id != id, ct))
            throw new BadRequestException($"A coupon with code \"{code}\" already exists.");

        Apply(coupon, request);
        await _db.SaveChangesAsync(ct);
        return ToDto(coupon);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (coupon is null) return false;
        _db.Coupons.Remove(coupon);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void Apply(Coupon coupon, CouponUpsertRequest request)
    {
        coupon.Code = CouponRules.NormalizeCode(request.Code);
        coupon.Type = request.Type;
        coupon.Value = request.Value;
        coupon.MinOrderAmount = request.MinOrderAmount;
        coupon.MaxUses = request.MaxUses;
        coupon.ValidFrom = request.ValidFrom;
        coupon.ValidTo = request.ValidTo;
        coupon.IsActive = request.IsActive;
    }

    private static AdminCouponDto ToDto(Coupon c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Type = c.Type,
        Value = c.Value,
        MinOrderAmount = c.MinOrderAmount,
        MaxUses = c.MaxUses,
        UsedCount = c.UsedCount,
        ValidFrom = c.ValidFrom,
        ValidTo = c.ValidTo,
        IsActive = c.IsActive
    };
}
