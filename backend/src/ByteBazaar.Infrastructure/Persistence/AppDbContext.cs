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
    }
}
