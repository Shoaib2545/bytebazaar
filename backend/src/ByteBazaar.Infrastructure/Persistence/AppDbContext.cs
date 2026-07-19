using System.Linq.Expressions;
using System.Text.Json;
using ByteBazaar.Application.Abstractions;
using ByteBazaar.Domain.Entities;
using ByteBazaar.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ByteBazaar.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Banner> Banners => Set<Banner>();

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        if (Database.IsNpgsql())
        {
            var strategy = Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
                await operation(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });
        }
        else
        {
            // InMemory provider has no transactions; operations are written to defer
            // SaveChanges until fully validated, which gives equivalent semantics.
            await operation(cancellationToken);
        }
    }

    public async Task<List<Guid>> DecrementStockAsync(
        IReadOnlyList<(Guid ProductId, int Quantity)> lines, CancellationToken cancellationToken = default)
    {
        var failed = new List<Guid>();

        if (Database.IsNpgsql())
        {
            // Set-based conditional updates guarded by stock >= quantity; the caller runs this
            // inside a transaction and throws on any failure so partial decrements roll back.
            foreach (var (productId, quantity) in lines)
            {
                var updated = await Products
                    .Where(p => p.Id == productId && p.Stock >= quantity)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - quantity), cancellationToken);
                if (updated == 0)
                    failed.Add(productId);
            }
        }
        else
        {
            // InMemory emulation: validate every line first, write nothing when any fails.
            var ids = lines.Select(l => l.ProductId).ToList();
            var products = await Products
                .Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            foreach (var (productId, quantity) in lines)
            {
                if (!products.TryGetValue(productId, out var product) || product.Stock < quantity)
                    failed.Add(productId);
            }

            if (failed.Count == 0)
            {
                foreach (var (productId, quantity) in lines)
                    products[productId].Stock -= quantity;
                await SaveChangesAsync(cancellationToken);
            }
        }

        return failed;
    }

    public async Task RestoreStockAsync(
        IReadOnlyList<(Guid ProductId, int Quantity)> lines, CancellationToken cancellationToken = default)
    {
        if (Database.IsNpgsql())
        {
            foreach (var (productId, quantity) in lines)
            {
                await Products
                    .Where(p => p.Id == productId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + quantity), cancellationToken);
            }
        }
        else
        {
            var ids = lines.Select(l => l.ProductId).ToList();
            var products = await Products
                .Where(p => ids.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            foreach (var (productId, quantity) in lines)
            {
                if (products.TryGetValue(productId, out var product))
                    product.Stock += quantity;
            }
            await SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> TryTransitionOrderStatusAsync(
        Guid orderId, Domain.OrderStatus expectedStatus, Domain.OrderStatus newStatus, CancellationToken cancellationToken = default)
    {
        if (Database.IsNpgsql())
        {
            // Atomic claim: a concurrent transaction that already moved the order out of
            // expectedStatus makes this match 0 rows (after its commit releases the row lock).
            var updated = await Orders
                .Where(o => o.Id == orderId && o.Status == expectedStatus)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, newStatus), cancellationToken);
            return updated > 0;
        }

        // InMemory emulation: verify only — the caller mutates the tracked entity and saves,
        // preserving rollback-on-throw semantics (nothing is written unless SaveChanges runs).
        return await Orders.AnyAsync(o => o.Id == orderId && o.Status == expectedStatus, cancellationToken);
    }

    public async Task<bool> TryIncrementCouponUsageAsync(Guid couponId, CancellationToken cancellationToken = default)
    {
        if (Database.IsNpgsql())
        {
            var updated = await Coupons
                .Where(c => c.Id == couponId && (c.MaxUses == null || c.UsedCount < c.MaxUses))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedCount, c => c.UsedCount + 1), cancellationToken);
            return updated > 0;
        }

        // InMemory emulation: mutate the tracked entity; the caller's SaveChanges persists it
        // (deferred so a later throw in the same operation leaves nothing written).
        var coupon = await Coupons.FirstOrDefaultAsync(c => c.Id == couponId, cancellationToken);
        if (coupon is null || (coupon.MaxUses is not null && coupon.UsedCount >= coupon.MaxUses))
            return false;
        coupon.UsedCount++;
        return true;
    }

    public Expression<Func<Product, bool>> BuildAttributeFilter(string code, IReadOnlyList<string> values)
    {
        var parameter = Expression.Parameter(typeof(Product), "p");
        var attributes = Expression.Property(parameter, nameof(Product.Attributes));
        Expression? orChain = null;

        if (Database.IsNpgsql())
        {
            // attributes @> '{"code":"value"}' — null-safe, translatable, and served by the GIN index.
            var jsonContains = typeof(NpgsqlJsonDbFunctionsExtensions).GetMethod(
                nameof(NpgsqlJsonDbFunctionsExtensions.JsonContains),
                new[] { typeof(DbFunctions), typeof(object), typeof(object) })!;

            foreach (var value in values)
            {
                var json = JsonSerializer.Serialize(new Dictionary<string, string> { [code] = value });
                var call = Expression.Call(jsonContains,
                    Expression.Constant(EF.Functions),
                    attributes,
                    Expression.Constant(json, typeof(object)));
                orChain = orChain is null ? call : Expression.OrElse(orChain, call);
            }
        }
        else
        {
            // In-memory evaluation: guard with ContainsKey to avoid KeyNotFoundException.
            var indexer = typeof(Dictionary<string, string>).GetProperty("Item")!;
            var access = Expression.MakeIndex(attributes, indexer, new[] { Expression.Constant(code) });

            foreach (var value in values)
            {
                var equal = Expression.Equal(access, Expression.Constant(value, typeof(string)));
                orChain = orChain is null ? equal : Expression.OrElse(orChain, equal);
            }

            var containsKey = Expression.Call(
                attributes,
                typeof(Dictionary<string, string>).GetMethod(nameof(Dictionary<string, string>.ContainsKey))!,
                Expression.Constant(code));
            orChain = Expression.AndAlso(containsKey, orChain!);
        }

        return Expression.Lambda<Func<Product, bool>>(orChain!, parameter);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var isNpgsql = Database.IsNpgsql();
        var jsonOptions = JsonSerializerOptions.Default;

        var dictionaryConverter = new ValueConverter<Dictionary<string, string>, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, jsonOptions) ?? new Dictionary<string, string>());

        var dictionaryComparer = new ValueComparer<Dictionary<string, string>>(
            (a, b) => (a ?? new Dictionary<string, string>()).OrderBy(kv => kv.Key)
                .SequenceEqual((b ?? new Dictionary<string, string>()).OrderBy(kv => kv.Key)),
            v => v.Aggregate(0, (hash, kv) => HashCode.Combine(hash, kv.Key.GetHashCode(), kv.Value.GetHashCode())),
            v => new Dictionary<string, string>(v));

        var listConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>());

        var listComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            v => new List<string>(v));

        builder.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.Property(c => c.Name).HasMaxLength(200).IsRequired();
            e.Property(c => c.Slug).HasMaxLength(200).IsRequired();
            e.Property(c => c.ImageUrl).HasMaxLength(500);
            e.Property(c => c.MetaTitle).HasMaxLength(200);
            e.Property(c => c.MetaDescription).HasMaxLength(500);
            e.HasIndex(c => c.Slug).IsUnique();
            e.HasOne(c => c.Parent)
                .WithMany(c => c.Children)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AttributeDefinition>(e =>
        {
            e.ToTable("AttributeDefinitions");
            e.Property(a => a.Name).HasMaxLength(200).IsRequired();
            e.Property(a => a.Code).HasMaxLength(100).IsRequired();
            e.HasIndex(a => new { a.CategoryId, a.Code }).IsUnique();
            e.HasOne(a => a.Category)
                .WithMany(c => c.Attributes)
                .HasForeignKey(a => a.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            var options = e.Property(a => a.Options);
            if (isNpgsql)
            {
                options.HasColumnType("jsonb");
            }
            else
            {
                options.HasConversion(listConverter);
            }
            options.Metadata.SetValueComparer(listComparer);
        });

        builder.Entity<Brand>(e =>
        {
            e.ToTable("Brands");
            e.Property(b => b.Name).HasMaxLength(200).IsRequired();
            e.Property(b => b.Slug).HasMaxLength(200).IsRequired();
            e.Property(b => b.LogoUrl).HasMaxLength(500);
            e.HasIndex(b => b.Slug).IsUnique();
        });

        builder.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.Property(p => p.Name).HasMaxLength(300).IsRequired();
            e.Property(p => p.Slug).HasMaxLength(300).IsRequired();
            e.Property(p => p.Price).HasPrecision(12, 2);
            e.Property(p => p.SalePrice).HasPrecision(12, 2);
            e.Property(p => p.MetaTitle).HasMaxLength(200);
            e.Property(p => p.MetaDescription).HasMaxLength(500);
            e.HasIndex(p => p.Slug).IsUnique();
            e.HasIndex(p => p.CategoryId);
            e.HasIndex(p => p.Status);
            e.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Brand)
                .WithMany(b => b.Products)
                .HasForeignKey(p => p.BrandId)
                .OnDelete(DeleteBehavior.SetNull);

            var attributes = e.Property(p => p.Attributes);
            if (isNpgsql)
            {
                attributes.HasColumnType("jsonb");
                e.HasIndex(p => p.Attributes).HasMethod("gin");
            }
            else
            {
                attributes.HasConversion(dictionaryConverter);
            }
            attributes.Metadata.SetValueComparer(dictionaryComparer);
        });

        builder.Entity<ProductImage>(e =>
        {
            e.ToTable("ProductImages");
            e.Property(i => i.Url).HasMaxLength(500).IsRequired();
            e.HasOne(i => i.Product)
                .WithMany(p => p.Images)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshTokens");
            e.Property(t => t.Token).HasMaxLength(200).IsRequired();
            e.HasIndex(t => t.Token).IsUnique();
            e.HasIndex(t => t.UserId);
        });

        builder.Entity<AppUser>(e =>
        {
            // Default true so the migration backfills existing users as active
            // (otherwise the seeded admin would be locked out).
            e.Property(u => u.IsActive).HasDefaultValue(true);
        });

        builder.Entity<Coupon>(e =>
        {
            e.ToTable("Coupons");
            e.Property(c => c.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(c => c.Code).IsUnique();
            e.Property(c => c.Value).HasPrecision(12, 2);
            e.Property(c => c.MinOrderAmount).HasPrecision(12, 2);
        });

        builder.Entity<Banner>(e =>
        {
            e.ToTable("Banners");
            e.Property(b => b.Title).HasMaxLength(200).IsRequired();
            e.Property(b => b.Subtitle).HasMaxLength(500);
            e.Property(b => b.ImageUrl).HasMaxLength(500).IsRequired();
            e.Property(b => b.LinkUrl).HasMaxLength(500);
            e.HasIndex(b => new { b.Placement, b.SortOrder });
        });

        builder.Entity<Cart>(e =>
        {
            e.ToTable("Carts");
            e.Property(c => c.CouponCode).HasMaxLength(50);
            // Not unique: most rows have a NULL UserId (anonymous carts) and null-handling in
            // unique indexes differs between providers; one-cart-per-user is enforced in CartService.
            e.HasIndex(c => c.UserId);
            e.HasIndex(c => c.AnonymousId);
            e.HasMany(c => c.Items)
                .WithOne(i => i.Cart)
                .HasForeignKey(i => i.CartId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CartItem>(e =>
        {
            e.ToTable("CartItems");
            e.HasIndex(i => new { i.CartId, i.ProductId }).IsUnique();
            e.HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Order>(e =>
        {
            e.ToTable("Orders");
            e.Property(o => o.OrderNumber).HasMaxLength(20).IsRequired();
            e.HasIndex(o => o.OrderNumber).IsUnique();
            e.HasIndex(o => o.UserId);
            e.HasIndex(o => o.Status);
            e.HasIndex(o => o.CreatedAt);
            e.Property(o => o.Subtotal).HasPrecision(12, 2);
            e.Property(o => o.CouponCode).HasMaxLength(50);
            e.Property(o => o.Discount).HasPrecision(12, 2);
            e.Property(o => o.ShippingFee).HasPrecision(12, 2);
            e.Property(o => o.Total).HasPrecision(12, 2);
            e.Property(o => o.ShippingCode).HasMaxLength(50);
            e.Property(o => o.FullName).HasMaxLength(200).IsRequired();
            e.Property(o => o.Phone).HasMaxLength(30).IsRequired();
            e.Property(o => o.Email).HasMaxLength(256).IsRequired();
            e.Property(o => o.AddressLine).HasMaxLength(500).IsRequired();
            e.Property(o => o.City).HasMaxLength(100).IsRequired();
            e.Property(o => o.Region).HasMaxLength(100).IsRequired();
            e.Property(o => o.Notes).HasMaxLength(1000);
            e.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(o => o.History)
                .WithOne(h => h.Order)
                .HasForeignKey(h => h.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OrderItem>(e =>
        {
            e.ToTable("OrderItems");
            // ProductId is intentionally not a foreign key: order lines are immutable
            // snapshots and must survive product deletion.
            e.Property(i => i.ProductName).HasMaxLength(300).IsRequired();
            e.Property(i => i.ProductSlug).HasMaxLength(300).IsRequired();
            e.Property(i => i.ImageUrl).HasMaxLength(500);
            e.Property(i => i.UnitPrice).HasPrecision(12, 2);
            e.HasIndex(i => i.ProductId);
        });

        builder.Entity<OrderStatusHistory>(e =>
        {
            e.ToTable("OrderStatusHistories");
            e.Property(h => h.Note).HasMaxLength(500);
            e.HasIndex(h => h.OrderId);
        });

        builder.Entity<Address>(e =>
        {
            e.ToTable("Addresses");
            e.Property(a => a.FullName).HasMaxLength(200).IsRequired();
            e.Property(a => a.Phone).HasMaxLength(30).IsRequired();
            e.Property(a => a.AddressLine).HasMaxLength(500).IsRequired();
            e.Property(a => a.City).HasMaxLength(100).IsRequired();
            e.Property(a => a.Region).HasMaxLength(100).IsRequired();
            e.HasIndex(a => a.UserId);
        });

        builder.Entity<WishlistItem>(e =>
        {
            e.ToTable("WishlistItems");
            e.HasIndex(w => new { w.UserId, w.ProductId }).IsUnique();
            e.HasOne(w => w.Product)
                .WithMany()
                .HasForeignKey(w => w.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
