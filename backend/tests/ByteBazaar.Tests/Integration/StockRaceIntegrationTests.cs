using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using ByteBazaar.Application.Services;
using ByteBazaar.Domain;
using ByteBazaar.Domain.Entities;
using ByteBazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Tests.Integration;

/// <summary>
/// PLAN.md M7: "two buyers, one unit". These run against real PostgreSQL because the guard being
/// tested is a conditional set-based UPDATE whose whole value is the row lock it takes — the
/// InMemory provider's emulation (read, compare, write) has no lock at all and would pass these
/// tests even if the production path were broken.
///
/// IMPORTANT — where the stock guard actually is:
/// ByteBazaar reserves stock at ADMIN CONFIRMATION, not at checkout. CheckoutService validates
/// stock advisorily and creates a Pending order without decrementing anything;
/// AdminOrderService.TransitionStatusAsync(Confirmed) is what calls DecrementStockAsync. So the
/// oversell question has two halves and both are tested here.
/// </summary>
[Collection(PostgresCollection.Name)]
public class StockRaceIntegrationTests
{
    private readonly PostgresFixture _fixture;

    public StockRaceIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

    private static CheckoutService Checkout(AppDbContext db) =>
        new(db, new CartService(db), new DefaultShippingOptionsProvider(), new FakeNotificationQueue());

    private static AdminOrderService AdminOrders(AppDbContext db) =>
        new(db, new FakeNotificationQueue(), new FakeRevalidator());

    private static CheckoutRequest Request(string name) => new()
    {
        FullName = name,
        Phone = "0301-1234567",
        Email = $"{name.ToLowerInvariant()}@example.com",
        AddressLine = "House 1",
        City = "Karachi",
        Region = "Sindh",
        ShippingCode = "standard",
        PaymentMethod = PaymentMethod.COD
    };

    /// <summary>
    /// THE CORE ASSERTION: two admins confirming two different orders for the same single unit,
    /// concurrently, against real Postgres. Exactly one must win and stock must never go negative.
    /// </summary>
    [Fact]
    public async Task TwoConfirmations_ForTheLastUnit_OnlyOneSucceeds_AndStockNeverGoesNegative()
    {
        var database = await _fixture.CreateDatabaseAsync();

        Guid productId;
        Guid orderA, orderB;
        await using (var db = _fixture.CreateContext(database))
        {
            await IntegrationSeed.SeedCatalogAsync(db);
            var product = await IntegrationSeed.AddProductAsync(db, "Laptop One", "laptop-one", 100000m, stock: 1);
            productId = product.Id;

            orderA = await AddPendingOrderAsync(db, "BB-000001", productId, quantity: 1);
            orderB = await AddPendingOrderAsync(db, "BB-000002", productId, quantity: 1);
        }

        // Two independent contexts => two independent connections => two real transactions.
        await using var dbA = _fixture.CreateContext(database);
        await using var dbB = _fixture.CreateContext(database);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Exception?> Confirm(AppDbContext db, Guid orderId)
        {
            await gate.Task;
            try
            {
                await AdminOrders(db).TransitionStatusAsync(
                    orderId, new OrderStatusUpdateRequest { Status = OrderStatus.Confirmed });
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        var taskA = Task.Run(() => Confirm(dbA, orderA));
        var taskB = Task.Run(() => Confirm(dbB, orderB));
        gate.SetResult();
        var results = await Task.WhenAll(taskA, taskB);

        var succeeded = results.Count(r => r is null);
        var failed = results.Where(r => r is not null).ToList();

        await using var verify = _fixture.CreateContext(database);
        var finalStock = await verify.Products.AsNoTracking().Where(p => p.Id == productId)
            .Select(p => p.Stock).SingleAsync();
        var confirmed = await verify.Orders.AsNoTracking()
            .CountAsync(o => o.Status == OrderStatus.Confirmed);

        // Oversell prevention: never negative, and never two confirmations off one unit.
        Assert.True(finalStock >= 0, $"Stock went negative ({finalStock}) — oversell.");
        Assert.Equal(0, finalStock);
        Assert.Equal(1, succeeded);
        Assert.Equal(1, confirmed);

        // The loser must fail for a stock reason, not a random error.
        var loser = Assert.Single(failed);
        Assert.True(
            loser is StockConflictException or OrderConflictException or DbUpdateException,
            $"Unexpected loser exception: {loser!.GetType().Name}: {loser.Message}");
    }

    /// <summary>
    /// Ten concurrent confirmations against three units. Exactly three may win. This is the test
    /// that would catch a read-then-write guard masquerading as an atomic one.
    /// </summary>
    [Fact]
    public async Task TenConcurrentConfirmations_AgainstThreeUnits_ConfirmExactlyThree()
    {
        const int stock = 3;
        const int contenders = 10;
        var database = await _fixture.CreateDatabaseAsync();

        Guid productId;
        var orderIds = new List<Guid>();
        await using (var db = _fixture.CreateContext(database))
        {
            await IntegrationSeed.SeedCatalogAsync(db);
            var product = await IntegrationSeed.AddProductAsync(db, "Laptop Ten", "laptop-ten", 50000m, stock);
            productId = product.Id;
            for (var i = 0; i < contenders; i++)
                orderIds.Add(await AddPendingOrderAsync(db, $"BB-{i + 1:D6}", productId, quantity: 1));
        }

        var contexts = orderIds.Select(_ => _fixture.CreateContext(database)).ToList();
        try
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var tasks = orderIds.Select((orderId, i) => Task.Run(async () =>
            {
                await gate.Task;
                try
                {
                    await AdminOrders(contexts[i]).TransitionStatusAsync(
                        orderId, new OrderStatusUpdateRequest { Status = OrderStatus.Confirmed });
                    return true;
                }
                catch
                {
                    return false;
                }
            })).ToList();

            gate.SetResult();
            var results = await Task.WhenAll(tasks);

            await using var verify = _fixture.CreateContext(database);
            var finalStock = await verify.Products.AsNoTracking().Where(p => p.Id == productId)
                .Select(p => p.Stock).SingleAsync();
            var confirmed = await verify.Orders.AsNoTracking().CountAsync(o => o.Status == OrderStatus.Confirmed);

            Assert.True(finalStock >= 0, $"Stock went negative ({finalStock}) — oversell.");
            Assert.Equal(0, finalStock);
            Assert.Equal(stock, confirmed);
            Assert.Equal(stock, results.Count(ok => ok));
        }
        finally
        {
            foreach (var context in contexts) await context.DisposeAsync();
        }
    }

    /// <summary>
    /// Documents the checkout half honestly. Two buyers racing for one unit BOTH get a Pending
    /// order: checkout's stock check is advisory and decrements nothing. That is by design
    /// (reserve-at-confirm), and the confirmation tests above are what stop the oversell — but it
    /// means "order placed" is not a stock reservation, and the second buyer finds out later.
    /// </summary>
    [Fact]
    public async Task TwoBuyers_CheckingOutTheLastUnit_BothGetPendingOrders_StockIsNotReserved()
    {
        var database = await _fixture.CreateDatabaseAsync();
        var buyerA = Guid.NewGuid();
        var buyerB = Guid.NewGuid();

        Guid productId;
        await using (var db = _fixture.CreateContext(database))
        {
            await IntegrationSeed.SeedCatalogAsync(db);
            var product = await IntegrationSeed.AddProductAsync(db, "Laptop Last", "laptop-last", 100000m, stock: 1);
            productId = product.Id;

            await new CartService(db).AddItemAsync(null, buyerA,
                new AddCartItemRequest { ProductId = productId, Quantity = 1 });
            await new CartService(db).AddItemAsync(null, buyerB,
                new AddCartItemRequest { ProductId = productId, Quantity = 1 });
        }

        await using var dbA = _fixture.CreateContext(database);
        await using var dbB = _fixture.CreateContext(database);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Place(AppDbContext db, Guid anonymousId, string name)
        {
            await gate.Task;
            try
            {
                await Checkout(db).CheckoutAsync(null, anonymousId, Request(name));
                return true;
            }
            catch
            {
                return false;
            }
        }

        var placeA = Task.Run(() => Place(dbA, buyerA, "Ali"));
        var placeB = Task.Run(() => Place(dbB, buyerB, "Bilal"));
        gate.TrySetResult();
        var results = await Task.WhenAll(placeA, placeB);

        await using var verify = _fixture.CreateContext(database);
        var stock = await verify.Products.AsNoTracking().Where(p => p.Id == productId)
            .Select(p => p.Stock).SingleAsync();
        var orders = await verify.Orders.AsNoTracking().CountAsync();

        // Stock is untouched by checkout — this is the reserve-at-confirm design.
        Assert.Equal(1, stock);

        // At least one checkout succeeds. Both usually do; the order-number generator
        // (max+1 under a unique index) can make the loser fail with a duplicate key instead,
        // which is a separate, documented race — see the OrderNumber test below.
        Assert.True(results.Any(ok => ok), "Neither buyer could check out.");
        Assert.Equal(results.Count(ok => ok), orders);
    }

    /// <summary>
    /// The order-number generator reads MAX(OrderNumber)+1 outside any lock, so two simultaneous
    /// checkouts can both compute the same number. The unique index is the backstop: one commits,
    /// the other gets a duplicate-key failure and NO partial order. This test asserts the safety
    /// property (never two orders with one number, never a half-written order), not that both
    /// checkouts succeed — because under a true tie one legitimately must not.
    /// </summary>
    [Fact]
    public async Task ConcurrentCheckouts_NeverProduceDuplicateOrderNumbers()
    {
        const int buyers = 6;
        var database = await _fixture.CreateDatabaseAsync();
        var anonymousIds = Enumerable.Range(0, buyers).Select(_ => Guid.NewGuid()).ToList();

        await using (var db = _fixture.CreateContext(database))
        {
            await IntegrationSeed.SeedCatalogAsync(db);
            var product = await IntegrationSeed.AddProductAsync(db, "Laptop Many", "laptop-many", 10000m, stock: 100);
            foreach (var id in anonymousIds)
            {
                await new CartService(db).AddItemAsync(null, id,
                    new AddCartItemRequest { ProductId = product.Id, Quantity = 1 });
            }
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
                    await Checkout(contexts[i]).CheckoutAsync(null, anonymousId, Request($"Buyer{i}"));
                    return true;
                }
                catch
                {
                    return false;
                }
            })).ToList();

            gate.SetResult();
            var results = await Task.WhenAll(tasks);

            await using var verify = _fixture.CreateContext(database);
            var numbers = await verify.Orders.AsNoTracking().Select(o => o.OrderNumber).ToListAsync();
            var itemCount = await verify.OrderItems.AsNoTracking().CountAsync();

            Assert.Equal(numbers.Count, numbers.Distinct().Count());
            Assert.Equal(results.Count(ok => ok), numbers.Count);
            // Every committed order has its line — no torn writes.
            Assert.Equal(numbers.Count, itemCount);
        }
        finally
        {
            foreach (var context in contexts) await context.DisposeAsync();
        }
    }

    private static async Task<Guid> AddPendingOrderAsync(
        AppDbContext db, string orderNumber, Guid productId, int quantity)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            Status = OrderStatus.Pending,
            PaymentMethod = PaymentMethod.COD,
            Subtotal = 100000m,
            ShippingFee = 250m,
            Total = 100250m,
            ShippingCode = "standard",
            FullName = "Race Buyer",
            Phone = "0301-1234567",
            Email = "race@example.com",
            AddressLine = "House 1",
            City = "Karachi",
            Region = "Sindh",
            CreatedAt = DateTime.UtcNow
        };
        order.Items.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = productId,
            ProductName = "Laptop",
            ProductSlug = "laptop",
            UnitPrice = 100000m,
            Quantity = quantity
        });
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }
}
