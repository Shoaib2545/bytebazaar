using System.Text.Json;
using ByteBazaar.Application.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ByteBazaar.Infrastructure.Caching;

/// <summary>
/// <see cref="ICacheStore"/> over <see cref="IDistributedCache"/> (Redis when the connection
/// string is configured) with an in-process <see cref="IMemoryCache"/> fallback. If Redis is
/// unreachable at runtime the first failure flips this instance to the memory cache for a short
/// window, so a Redis outage degrades to in-process caching instead of failing requests.
/// </summary>
public class DistributedCacheStore : ICacheStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _distributed;
    private readonly IMemoryCache _memory;
    private readonly ILogger<DistributedCacheStore> _logger;
    private readonly TimeSpan _breakDuration = TimeSpan.FromSeconds(30);

    private DateTime _distributedUnavailableUntil = DateTime.MinValue;

    public DistributedCacheStore(
        IDistributedCache distributed,
        IMemoryCache memory,
        ILogger<DistributedCacheStore> logger)
    {
        _distributed = distributed;
        _memory = memory;
        _logger = logger;
    }

    private bool DistributedAvailable => DateTime.UtcNow >= _distributedUnavailableUntil;

    public async Task<T> GetOrSetAsync<T>(
        string key, Func<CancellationToken, Task<T>> factory, TimeSpan ttl, CancellationToken ct = default)
    {
        if (DistributedAvailable)
        {
            try
            {
                var cached = await _distributed.GetStringAsync(key, ct);
                if (cached is not null)
                {
                    var value = JsonSerializer.Deserialize<T>(cached, SerializerOptions);
                    if (value is not null) return value;
                }

                var fresh = await factory(ct);
                await _distributed.SetStringAsync(
                    key,
                    JsonSerializer.Serialize(fresh, SerializerOptions),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                    ct);
                return fresh;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Trip(ex);
            }
        }

        // Fallback path: in-process cache. Also used while the circuit is open.
        if (_memory.TryGetValue(key, out T? memoryHit) && memoryHit is not null) return memoryHit;
        var computed = await factory(ct);
        _memory.Set(key, computed, ttl);
        return computed;
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _memory.Remove(key);
        if (!DistributedAvailable) return;

        try
        {
            await _distributed.RemoveAsync(key, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Trip(ex);
        }
    }

    private void Trip(Exception ex)
    {
        _distributedUnavailableUntil = DateTime.UtcNow.Add(_breakDuration);
        _logger.LogWarning(ex,
            "Distributed cache (Redis) unavailable; using the in-memory cache for the next {Seconds}s.",
            _breakDuration.TotalSeconds);
    }
}
