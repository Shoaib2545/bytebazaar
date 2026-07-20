using ByteBazaar.Application.Abstractions;
using Microsoft.AspNetCore.OutputCaching;

namespace ByteBazaar.Api.Services;

/// <summary>
/// Evicts output-cache entries by tag after admin catalog writes. Failures are logged, never
/// propagated — a stale cache entry expires on its own within the policy TTL.
/// </summary>
public class OutputCacheInvalidator : IOutputCacheInvalidator
{
    private readonly IOutputCacheStore _store;
    private readonly ILogger<OutputCacheInvalidator> _logger;

    public OutputCacheInvalidator(IOutputCacheStore store, ILogger<OutputCacheInvalidator> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task EvictAsync(params string[] tags)
    {
        foreach (var tag in tags)
        {
            try
            {
                await _store.EvictByTagAsync(tag, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evict output-cache tag {Tag}.", tag);
            }
        }
    }
}
