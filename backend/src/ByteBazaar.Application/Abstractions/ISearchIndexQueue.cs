namespace ByteBazaar.Application.Abstractions;

/// <summary>
/// Dispatches search re-indexing work. The Infrastructure implementation enqueues Hangfire jobs
/// when Hangfire is available (DB reachable at startup) and otherwise runs the work inline.
/// Like <see cref="IOrderNotificationQueue"/> it never throws — indexing is best-effort.
/// </summary>
public interface ISearchIndexQueue
{
    /// <summary>Re-index a single product after a create/update.</summary>
    Task EnqueueProductIndexAsync(Guid productId);

    /// <summary>Drop a product document after a delete (or an unpublish).</summary>
    Task EnqueueProductDeleteAsync(Guid productId);

    /// <summary>Rebuild the whole product index from the database.</summary>
    Task EnqueueFullReindexAsync();
}
