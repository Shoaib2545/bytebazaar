using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers;

/// <summary>Public redirect lookup consumed by the storefront's middleware.</summary>
[ApiController]
[Route("api/redirects")]
public class RedirectsController : ControllerBase
{
    private readonly RedirectService _service;

    public RedirectsController(RedirectService service)
    {
        _service = service;
    }

    /// <summary>
    /// Resolves a single path. 404 when no active rule matches — the middleware should treat that
    /// as "carry on", and any error as "carry on" too (redirects must never break the site).
    /// </summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<RedirectLookupDto>> Lookup([FromQuery] string path, CancellationToken ct)
    {
        var redirect = await _service.LookupAsync(path, ct);
        return redirect is null ? NotFound() : Ok(redirect);
    }

    /// <summary>All active rules, so the storefront can preload the (small) set at boot.</summary>
    [HttpGet]
    public async Task<ActionResult<List<AdminRedirectDto>>> GetActive(CancellationToken ct)
        => Ok(await _service.GetActiveAsync(ct));
}
