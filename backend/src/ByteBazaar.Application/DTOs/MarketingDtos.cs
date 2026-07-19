using ByteBazaar.Domain;

namespace ByteBazaar.Application.DTOs;

// ----- Coupons (admin) -----

public class AdminCouponDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public CouponType Type { get; set; }
    public decimal Value { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public int? MaxUses { get; set; }
    public int UsedCount { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; }
}

public class CouponUpsertRequest
{
    public string Code { get; set; } = string.Empty;
    public CouponType Type { get; set; } = CouponType.Percent;
    public decimal Value { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public int? MaxUses { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
}

// ----- Banners -----

/// <summary>Public storefront shape (only active banners within their window are served).</summary>
public class BannerDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public BannerPlacement Placement { get; set; }
    public int SortOrder { get; set; }
}

public class AdminBannerDto : BannerDto
{
    public bool IsActive { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
}

public class BannerUpsertRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public BannerPlacement Placement { get; set; } = BannerPlacement.Hero;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
}
