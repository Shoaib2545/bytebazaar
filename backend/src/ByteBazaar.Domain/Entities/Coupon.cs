namespace ByteBazaar.Domain.Entities;

public class Coupon
{
    public Guid Id { get; set; }

    /// <summary>Always stored uppercase; lookups normalize input before comparing.</summary>
    public string Code { get; set; } = string.Empty;

    public CouponType Type { get; set; } = CouponType.Percent;
    public decimal Value { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public int? MaxUses { get; set; }
    public int UsedCount { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
}
