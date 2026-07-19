namespace ByteBazaar.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;
    public decimal Subtotal { get; set; }
    public string? CouponCode { get; set; }
    public decimal Discount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
    public string ShippingCode { get; set; } = string.Empty;

    // Shipping address / guest contact snapshot
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<OrderItem> Items { get; set; } = new();
    public List<OrderStatusHistory> History { get; set; } = new();
}
