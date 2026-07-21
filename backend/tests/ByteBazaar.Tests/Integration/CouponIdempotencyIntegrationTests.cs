using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using ByteBazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Tests.Integration;

/// <summary>
/// ByteBazaar has no payment gateway or webhook yet (COD only — see PLAN.md M4/M5), so there is no
/// payment-idempotency path to test. Its structural equivalent — the one place where a
/// limited-use resource is claimed exactly once under concurrency — is the coupon usage counter,
/// so that is what is verified here against real PostgreSQL.
///
/// TryIncrementCouponUsageAsync is a conditional set-based UPDATE (WHERE UsedCount &lt; MaxUses).
/// The InMemory emulation reads-then-mutates, so it cannot demonstrate the guard actually holds.
/// </summary>
[Collection(PostgresCollection.Name)]
public class CouponIdempotencyIntegrationTests
{
    private readonly PostgresFixture _fixture;

    public CouponIdempotencyIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

    private static CheckoutService Checkout(AppDbContext db) =>
        new(db, new CartService(db), new DefaultShippingOptionsProvider(), new FakeNotificationQueue());

    [Fact]
    public async Task SingleUseCoupon_ConcurrentCheckouts_IsClaimedExactlyOnce()
    {
        const int buyers = 5;
        var database = await _fixture.CreateDatabaseAsync();
        var anonymousIds = Enumerable.Range(0, buyers).Select(_ => Guid.NewGuid()).ToList();

        await using (var db = _fixture.CreateContext(database))
        {
            await IntegrationSeed.SeedCatalogAsync(db);
            var product = await IntegrationSeed.AddProductAsync(db, "Laptop Coupon", "laptop-coupon", 100000m, 100);

            db.Coupons.Add(new Coupon
            {
                Id = Guid.NewGuid(),
                Code = "ONCE",
                Type = CouponType.Fixed,
                Value = 5000m,
                MaxUses = 1,
                UsedCount = 0,
                IsActive = true,
                ValidFrom = DateTime.UtcNow.AddDays(-1),
                ValidTo = DateTime.UtcNow.AddDays(30)
            });
            await db.SaveChangesAsync();

            var cartService = new CartService(db);
            foreach (var id in anonymousIds)
            {
                await cartService.AddItemAsync(null, id,
                    new AddCartItemRequest { ProductId = product.Id, Quantity = 1 });
            }

            // Attach the coupon directly — every cart legitimately holds it at the moment of the
            // race; the contended resource is the single usage claimed inside checkout.
            foreach (var cart in await db.Carts.ToListAsync())
                cart.CouponCode = "ONCE";
            await db.SaveChangesAsync();
        }

        var contexts = anonymousIds.Select(_ => _fixture.CreateContext(database)).ToList();
        try
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tasks = anonymousIds.Select((anonymousId, i) => Task.Run(async () =>
            {
                await gate.Task;
                try
                {
                    var result = await Checkout(contexts[i]).CheckoutAsync(null, anonymousId, new CheckoutRequest
                    {
                        FullName = $"Buyer{i}",
                        Phone = "0301-1234567",
                        Email = $"buyer{i}@example.com",
                        AddressLine = "House 1",
                        City = "Karachi",
                        Region = "Sindh",
                        ShippingCode = "standard",
                        PaymentMethod = PaymentMethod.COD
                    });
                    return result.Discount > 0;
                }
                catch
                {
                    return false;
                }
            })).ToList();

            gate.SetResult();
            var results = await Task.WhenAll(tasks);

            await using var verify = _fixture.CreateContext(database);
            var coupon = await verify.Coupons.AsNoTracking().SingleAsync(c => c.Code == "ONCE");
            var discountedOrders = await verify.Orders.AsNoTracking().CountAsync(o => o.Discount > 0);

            // The counter must never exceed MaxUses, and exactly one order may carry the discount.
            Assert.Equal(1, coupon.UsedCount);
            Assert.Equal(1, discountedOrders);
            Assert.Equal(1, results.Count(discounted => discounted));
        }
        finally
        {
            foreach (var context in contexts) await context.DisposeAsync();
        }
    }
}
