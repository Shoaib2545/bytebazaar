using ByteBazaar.Infrastructure.Persistence;
using ByteBazaar.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ByteBazaar.Infrastructure.Health;

/// <summary>
/// Readiness model, and why the three datastores are not treated alike:
///
/// * <b>Postgres is fatal.</b> Every request path that matters (catalog, cart, checkout, auth)
///   reads from it. The API deliberately still *starts* when Postgres is down (see Program.cs) —
///   that is a startup-resilience decision, not a claim of readiness. This check is what makes
///   the two distinguishable to an orchestrator.
/// * <b>Redis is degraded-only.</b> DistributedCacheStore falls back to an in-process
///   IMemoryCache on the first failure, so a Redis outage costs cache hit-rate, not correctness.
/// * <b>Meilisearch is degraded-only.</b> SearchService falls back to the Postgres query path
///   when the index is unreachable, so search still answers — just less well.
///
/// Degraded maps to HTTP 200 in Program.cs on purpose: a degraded instance is still the best
/// instance available, and pulling it out of the load balancer because Redis blinked would turn
/// a cache outage into a site outage.
/// </summary>
public static class HealthCheckExtensions
{
    public const string ReadyTag = "ready";
    public const string LiveTag = "live";

    public static IServiceCollection AddByteBazaarHealthChecks(
        this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddHealthChecks();

        // Liveness: the process is up and the pipeline can answer. Intentionally touches nothing
        // external — a liveness probe that depends on a datastore causes restart storms during a
        // database incident, which is the worst possible time to be recycling containers.
        builder.AddCheck("self", () => HealthCheckResult.Healthy("Process is running."), tags: new[] { LiveTag });

        builder.AddCheck<PostgresHealthCheck>(
            "postgres", failureStatus: HealthStatus.Unhealthy, tags: new[] { ReadyTag });

        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("Redis")))
        {
            builder.AddCheck<RedisHealthCheck>(
                "redis", failureStatus: HealthStatus.Degraded, tags: new[] { ReadyTag });
        }

        if (!string.IsNullOrWhiteSpace(configuration.GetSection(MeilisearchOptions.SectionName)["Url"]))
        {
            builder.AddCheck<MeilisearchHealthCheck>(
                "meilisearch", failureStatus: HealthStatus.Degraded, tags: new[] { ReadyTag });
        }

        return services;
    }
}

/// <summary>
/// Proves the database is reachable AND migrated. CanConnectAsync alone is not enough: a fresh
/// container answers TCP long before the schema exists, and the startup migration is best-effort,
/// so "connected but unmigrated" is a real state this must not report as ready.
/// </summary>
public class PostgresHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;

    public PostgresHealthCheck(AppDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _db.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("Cannot connect to PostgreSQL.");

            var pending = (await _db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0)
            {
                return HealthCheckResult.Unhealthy(
                    $"Connected, but {pending.Count} migration(s) are not applied: {string.Join(", ", pending)}.");
            }

            // Cheapest query that proves the application schema (not just the EF history table)
            // is actually queryable.
            _ = await _db.Categories.AsNoTracking().Select(c => c.Id).FirstOrDefaultAsync(cancellationToken);
            return HealthCheckResult.Healthy("PostgreSQL reachable and migrated.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL check failed.", ex);
        }
    }
}

/// <summary>Round-trips one probe key. Failure is Degraded — see the class comment above.</summary>
public class RedisHealthCheck : IHealthCheck
{
    private const string ProbeKey = "bytebazaar:health:probe";

    private readonly IDistributedCache _cache;

    public RedisHealthCheck(IDistributedCache cache) => _cache = cache;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.SetStringAsync(
                ProbeKey,
                DateTime.UtcNow.ToString("O"),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) },
                cancellationToken);
            _ = await _cache.GetStringAsync(ProbeKey, cancellationToken);

            return HealthCheckResult.Healthy("Redis round-trip OK.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Redis unreachable; the API is serving from the in-process cache fallback.",
                ex);
        }
    }
}

/// <summary>Hits Meilisearch's own /health. Failure is Degraded — search falls back to Postgres.</summary>
public class MeilisearchHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MeilisearchOptions _options;

    public MeilisearchHealthCheck(IHttpClientFactory httpClientFactory, IOptions<MeilisearchOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Url))
            return HealthCheckResult.Healthy("Meilisearch is not configured; search uses the database.");

        try
        {
            var http = _httpClientFactory.CreateClient(MeilisearchSearchIndex.HttpClientName);
            var response = await http.GetAsync(
                _options.Url.TrimEnd('/') + "/health", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Meilisearch reachable.")
                : new HealthCheckResult(
                    context.Registration.FailureStatus,
                    $"Meilisearch returned {(int)response.StatusCode}; search falls back to the database.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Meilisearch unreachable; search falls back to the database.",
                ex);
        }
    }
}
