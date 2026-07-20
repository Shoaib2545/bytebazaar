using ByteBazaar.Application.Abstractions;
using Meilisearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ByteBazaar.Infrastructure.Search;

/// <summary>
/// Meilisearch-backed <see cref="ISearchIndex"/>. Degrades exactly like DbSeeder does when
/// Postgres is down: nothing here ever throws, so the API starts and serves traffic with the
/// database fallback path when Meilisearch is unreachable or unconfigured. After a failure a
/// short circuit-breaker window suppresses further calls so an outage costs one timeout rather
/// than one per request.
/// </summary>
public class MeilisearchSearchIndex : ISearchIndex
{
    private readonly MeilisearchOptions _options;
    private readonly ILogger<MeilisearchSearchIndex> _logger;
    private readonly MeilisearchClient? _client;
    private readonly SemaphoreSlim _setupLock = new(1, 1);

    private bool _settingsApplied;
    private DateTime _unavailableUntil = DateTime.MinValue;

    public MeilisearchSearchIndex(
        IOptions<MeilisearchOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<MeilisearchSearchIndex> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.Url))
        {
            _logger.LogInformation("Meilisearch is not configured; search falls back to the database.");
            return;
        }

        var http = httpClientFactory.CreateClient(HttpClientName);
        http.BaseAddress = new Uri(_options.Url.TrimEnd('/') + "/");
        _client = new MeilisearchClient(http, _options.ApiKey ?? string.Empty);
    }

    public const string HttpClientName = "meilisearch";

    private bool Available => _client is not null && DateTime.UtcNow >= _unavailableUntil;

    public async Task IndexProductsAsync(IReadOnlyList<SearchProductDocument> documents, CancellationToken ct = default)
    {
        if (documents.Count == 0 || !Available) return;
        await SafeAsync("index products", async () =>
        {
            await EnsureIndexAsync(ct);
            await _client!.Index(_options.IndexName).AddDocumentsAsync(documents, primaryKey: "id", ct);
        });
    }

    public async Task DeleteProductAsync(Guid productId, CancellationToken ct = default)
    {
        if (!Available) return;
        await SafeAsync("delete product document", async () =>
        {
            await EnsureIndexAsync(ct);
            await _client!.Index(_options.IndexName).DeleteOneDocumentAsync(productId.ToString("N"), ct);
        });
    }

    public async Task ResetProductsAsync(CancellationToken ct = default)
    {
        if (!Available) return;
        await SafeAsync("reset product index", async () =>
        {
            await EnsureIndexAsync(ct);
            await _client!.Index(_options.IndexName).DeleteAllDocumentsAsync(ct);
        });
    }

    public async Task<SearchIndexResult?> SearchAsync(string query, int offset, int limit, CancellationToken ct = default)
    {
        if (!Available) return null;

        try
        {
            var result = await _client!.Index(_options.IndexName).SearchAsync<SearchProductDocument>(
                query,
                new SearchQuery { Offset = offset, Limit = limit },
                ct);

            if (result is not SearchResult<SearchProductDocument> typed) return null;

            return new SearchIndexResult
            {
                TotalCount = typed.EstimatedTotalHits,
                Hits = typed.Hits.Select(ToHit).ToList()
            };
        }
        catch (Exception ex)
        {
            Trip(ex, "search");
            return null;
        }
    }

    private static SearchHit ToHit(SearchProductDocument d) => new()
    {
        Id = Guid.TryParse(d.Id, out var id) ? id : Guid.Empty,
        Name = d.Name,
        Slug = d.Slug,
        Price = d.Price,
        SalePrice = d.SalePrice,
        ImageUrl = d.ImageUrl,
        BrandName = d.BrandName,
        CategorySlug = d.CategorySlug,
        CategoryName = d.CategoryName,
        Stock = d.Stock
    };

    /// <summary>
    /// Creates the index and applies searchable/filterable/sortable settings once per process.
    /// Idempotent: an existing index makes CreateIndexAsync fail, which we swallow.
    /// </summary>
    private async Task EnsureIndexAsync(CancellationToken ct)
    {
        if (_settingsApplied) return;
        await _setupLock.WaitAsync(ct);
        try
        {
            if (_settingsApplied) return;

            try
            {
                await _client!.CreateIndexAsync(_options.IndexName, "id", ct);
            }
            catch (MeilisearchApiError)
            {
                // index_already_exists — expected on every start after the first.
            }

            await _client!.Index(_options.IndexName).UpdateSettingsAsync(new Settings
            {
                SearchableAttributes = new[] { "name", "brandName", "categoryName", "attributesText", "description" },
                FilterableAttributes = new FilterableAttribute[] { "brandSlug", "categorySlug", "price", "stock" },
                SortableAttributes = new[] { "price", "createdAt", "name" }
            }, ct);

            _settingsApplied = true;
        }
        finally
        {
            _setupLock.Release();
        }
    }

    private async Task SafeAsync(string operation, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Trip(ex, operation);
        }
    }

    /// <summary>Logs and opens the circuit so the next calls short-circuit instead of timing out.</summary>
    private void Trip(Exception ex, string operation)
    {
        _unavailableUntil = DateTime.UtcNow.AddSeconds(_options.CircuitBreakSeconds);
        _logger.LogWarning(ex,
            "Meilisearch {Operation} failed; suppressing search-engine calls for {Seconds}s and using the database.",
            operation, _options.CircuitBreakSeconds);
    }
}
