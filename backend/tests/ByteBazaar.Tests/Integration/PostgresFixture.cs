using ByteBazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ByteBazaar.Tests.Integration;

/// <summary>
/// A real PostgreSQL 16 in a container, shared by every integration test in the
/// <see cref="PostgresCollection"/>. This exists because the InMemory provider used by the rest of
/// the suite cannot reproduce the things these tests are about:
///   * row locking and conditional set-based UPDATEs under genuine concurrency;
///   * jsonb containment (<c>@&gt;</c>) and the GIN index that serves the dynamic filter engine
///     (see CLAUDE.md — the attribute predicate is provider-specific and only one of the two
///     branches was previously exercised).
/// Each test gets a freshly migrated database so nothing leaks between them.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("bytebazaar_it")
        .WithUsername("bytebazaar")
        .WithPassword("bytebazaar_it")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>A context pointed at <paramref name="databaseName"/> on the shared container.</summary>
    public AppDbContext CreateContext(string databaseName)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString) { Database = databaseName };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(builder.ConnectionString, npgsql => npgsql.ConfigureDataSource(ds => ds.EnableDynamicJson()))
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// Creates and migrates an isolated database. Returns its name so a test can open several
    /// independent contexts (and therefore several independent connections/transactions) against
    /// it — which is exactly what a real concurrency test needs.
    /// </summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = "it_" + Guid.NewGuid().ToString("N")[..16];

        await using (var admin = CreateContext("postgres"))
            await admin.Database.ExecuteSqlRawAsync($"CREATE DATABASE \"{name}\"");

        await using (var db = CreateContext(name))
            await db.Database.MigrateAsync();

        return name;
    }
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres-integration";
}
