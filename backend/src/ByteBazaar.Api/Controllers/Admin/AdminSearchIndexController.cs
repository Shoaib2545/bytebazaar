using ByteBazaar.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers.Admin;

/// <summary>Manual control over the Meilisearch product index.</summary>
[ApiController]
[Route("api/admin/search-index")]
[Authorize(Roles = "Admin,Staff")]
public class AdminSearchIndexController : ControllerBase
{
    private readonly ISearchIndexQueue _queue;

    public AdminSearchIndexController(ISearchIndexQueue queue)
    {
        _queue = queue;
    }

    /// <summary>
    /// Queues a full rebuild of the product index (Hangfire job when available, inline otherwise).
    /// Always 202 — a search engine that is down is not an admin-facing error.
    /// </summary>
    [HttpPost("reindex")]
    public async Task<IActionResult> Reindex()
    {
        await _queue.EnqueueFullReindexAsync();
        return Accepted(new { status = "queued" });
    }
}
