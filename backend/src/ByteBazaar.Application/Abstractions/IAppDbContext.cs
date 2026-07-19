using System.Linq.Expressions;
using ByteBazaar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<AttributeDefinition> AttributeDefinitions { get; }
    DbSet<Brand> Brands { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<OrderStatusHistory> OrderStatusHistories { get; }
    DbSet<Address> Addresses { get; }
    DbSet<WishlistItem> WishlistItems { get; }

    /// <summary>
    /// Builds a provider-appropriate predicate matching products whose Attributes dictionary
    /// holds one of <paramref name="values"/> under <paramref name="code"/> (OR within the
    /// attribute; callers AND across attributes). On Npgsql this translates to jsonb
    /// containment (@&gt;); on in-memory providers it evaluates the dictionary directly.
    /// </summary>
    Expression<Func<Product, bool>> BuildAttributeFilter(string code, IReadOnlyList<string> values);

    /// <summary>
    /// Runs <paramref name="operation"/> inside a database transaction on relational providers;
    /// on the InMemory provider the operation runs directly (changes only persist when the
    /// operation itself calls SaveChangesAsync, giving equivalent rollback-on-throw semantics).
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically decrements stock for every line, guarded by stock &gt;= quantity.
    /// Returns the product ids that could not be decremented (insufficient stock).
    /// On Npgsql each line is a conditional set-based UPDATE (callers must wrap the call in
    /// <see cref="ExecuteInTransactionAsync"/> and throw on failure to roll back); on the
    /// InMemory provider all lines are validated first and nothing is written when any fails.
    /// </summary>
    Task<List<Guid>> DecrementStockAsync(IReadOnlyList<(Guid ProductId, int Quantity)> lines, CancellationToken cancellationToken = default);

    /// <summary>Adds the quantities back to product stock (e.g. cancelling a confirmed order).</summary>
    Task RestoreStockAsync(IReadOnlyList<(Guid ProductId, int Quantity)> lines, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
