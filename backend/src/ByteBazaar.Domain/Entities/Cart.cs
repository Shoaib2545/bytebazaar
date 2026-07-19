namespace ByteBazaar.Domain.Entities;

public class Cart
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? AnonymousId { get; set; }
    public string? CouponCode { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<CartItem> Items { get; set; } = new();
}
