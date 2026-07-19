using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

public class CheckoutService
{
    private readonly IAppDbContext _db;
    private readonly CartService _cartService;
    private readonly IShippingOptionsProvider _shippingOptions;
    private readonly IOrderNotificationQueue _notifications;

    public CheckoutService(
        IAppDbContext db,
        CartService cartService,
        IShippingOptionsProvider shippingOptions,
        IOrderNotificationQueue notifications)
    {
        _db = db;
        _cartService = cartService;
        _shippingOptions = shippingOptions;
        _notifications = notifications;
    }

    public IReadOnlyList<ShippingOptionDto> GetShippingOptions() => _shippingOptions.GetOptions();

    public async Task<CheckoutResultDto> CheckoutAsync(
        Guid? userId, Guid? anonymousId, CheckoutRequest request, CancellationToken ct = default)
    {
        var shipping = _shippingOptions.GetOptions().FirstOrDefault(o => o.Code == request.ShippingCode)
            ?? throw new BadRequestException($"Unknown shipping option \"{request.ShippingCode}\".");

        var cart = await _cartService.FindCartAsync(userId, anonymousId, track: true, ct);
        if (cart is null || cart.Items.Count == 0)
            throw new BadRequestException("Your cart is empty.");

        var now = DateTime.UtcNow;
        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.Status == ProductStatus.Active)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                p.Price,
                p.SalePrice,
                p.SaleStart,
                p.SaleEnd,
                p.Stock,
                ImageUrl = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()
            })
            .ToListAsync(ct);

        Order order = null!;
        await _db.ExecuteInTransactionAsync(async innerCt =>
        {
            var items = new List<OrderItem>();
            foreach (var cartItem in cart.Items)
            {
                var product = products.FirstOrDefault(p => p.Id == cartItem.ProductId)
                    ?? throw new BadRequestException("An item in your cart is no longer available.");
                if (cartItem.Quantity > product.Stock)
                    throw new BadRequestException($"Only {product.Stock} unit(s) of \"{product.Name}\" in stock.");

                items.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductSlug = product.Slug,
                    ImageUrl = product.ImageUrl,
                    UnitPrice = ProductPricing.EffectiveUnitPrice(
                        product.Price, product.SalePrice, product.SaleStart, product.SaleEnd, now),
                    Quantity = cartItem.Quantity
                });
            }

            var subtotal = items.Sum(i => i.UnitPrice * i.Quantity);

            // Re-validate the cart's coupon against the final subtotal; the usage counter is
            // claimed atomically below (TryIncrementCouponUsageAsync) inside this transaction.
            Coupon? coupon = null;
            var discount = 0m;
            if (!string.IsNullOrEmpty(cart.CouponCode))
            {
                coupon = await _db.Coupons.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Code == cart.CouponCode, innerCt)
                    ?? throw new BadRequestException($"Coupon \"{cart.CouponCode}\" no longer exists. Remove it and try again.");

                var reason = CouponRules.GetRejectionReason(coupon, subtotal, DateTime.UtcNow);
                if (reason is not null)
                    throw new BadRequestException(reason);

                discount = CouponRules.ComputeDiscount(coupon, subtotal);
            }

            order = new Order
            {
                Id = Guid.NewGuid(),
                OrderNumber = await GenerateOrderNumberAsync(innerCt),
                UserId = userId,
                Status = OrderStatus.Pending,
                PaymentMethod = request.PaymentMethod,
                Subtotal = subtotal,
                CouponCode = coupon?.Code,
                Discount = discount,
                ShippingFee = shipping.Fee,
                Total = subtotal - discount + shipping.Fee,
                ShippingCode = shipping.Code,
                FullName = request.FullName,
                Phone = request.Phone,
                Email = request.Email,
                AddressLine = request.AddressLine,
                City = request.City,
                Region = request.Region,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow
            };
            foreach (var item in items)
            {
                item.OrderId = order.Id;
                order.Items.Add(item);
            }
            order.History.Add(new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Status = OrderStatus.Pending,
                Note = "Order placed.",
                CreatedAt = DateTime.UtcNow
            });

            // Claim a coupon use atomically (guarded by usedCount < maxUses); losing the
            // race throws inside the transaction so no order is created.
            if (coupon is not null && !await _db.TryIncrementCouponUsageAsync(coupon.Id, innerCt))
                throw new BadRequestException($"Coupon \"{coupon.Code}\" has reached its usage limit.");

            _db.Orders.Add(order);
            _db.CartItems.RemoveRange(cart.Items);
            cart.Items.Clear();
            cart.CouponCode = null;
            cart.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(innerCt);
        }, ct);

        await _notifications.EnqueueOrderPlacedAsync(order.OrderNumber, order.Email);

        return new CheckoutResultDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CouponCode = order.CouponCode,
            Discount = order.Discount,
            Total = order.Total,
            Status = order.Status
        };
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken ct)
    {
        // Zero-padded, so string ordering matches numeric ordering. The unique index on
        // OrderNumber backstops the rare concurrent-checkout race (mapped to 409).
        var last = await _db.Orders
            .OrderByDescending(o => o.OrderNumber)
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync(ct);

        var next = 1;
        if (last is not null && last.StartsWith("BB-") && int.TryParse(last[3..], out var n))
            next = n + 1;
        return $"BB-{next:D6}";
    }
}
