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

    /// <summary>
    /// Builds a provider-appropriate predicate matching products whose Attributes dictionary
    /// holds one of <paramref name="values"/> under <paramref name="code"/> (OR within the
    /// attribute; callers AND across attributes). On Npgsql this translates to jsonb
    /// containment (@&gt;); on in-memory providers it evaluates the dictionary directly.
    /// </summary>
    Expression<Func<Product, bool>> BuildAttributeFilter(string code, IReadOnlyList<string> values);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
