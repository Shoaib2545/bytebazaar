using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.Services;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ByteBazaar.Infrastructure.Search;

/// <summary>
/// Dispatches re-indexing through Hangfire when it is available (DB reachable at startup) and
/// inline otherwise — the same pattern as <see cref="Notifications.OrderNotificationQueue"/>.
/// Never throws: a failed dispatch must not fail the admin write that triggered it.
/// </summary>
public class SearchIndexQueue : ISearchIndexQueue
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SearchIndexQueue> _logger;

    public SearchIndexQueue(IServiceProvider serviceProvider, ILogger<SearchIndexQueue> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task EnqueueProductIndexAsync(Guid productId)
        => DispatchAsync(
            client => client.Enqueue<SearchIndexingService>(s => s.IndexProductAsync(productId, CancellationToken.None)),
            service => service.IndexProductAsync(productId));

    public Task EnqueueProductDeleteAsync(Guid productId)
        => DispatchAsync(
            client => client.Enqueue<SearchIndexingService>(s => s.DeleteProductAsync(productId, CancellationToken.None)),
            service => service.DeleteProductAsync(productId));

    public Task EnqueueFullReindexAsync()
        => DispatchAsync(
            client => client.Enqueue<SearchIndexingService>(s => s.ReindexAllAsync(CancellationToken.None)),
            async service => await service.ReindexAllAsync());

    private async Task DispatchAsync(Action<IBackgroundJobClient> enqueue, Func<SearchIndexingService, Task> inline)
    {
        try
        {
            var client = _serviceProvider.GetService<IBackgroundJobClient>();
            if (client is not null)
            {
                enqueue(client);
                return;
            }

            // No Hangfire: run inline on a fresh scope so we never reuse the caller's DbContext.
            using var scope = _serviceProvider.CreateScope();
            await inline(scope.ServiceProvider.GetRequiredService<SearchIndexingService>());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch search re-indexing; the index may be stale until the next full reindex.");
        }
    }
}
