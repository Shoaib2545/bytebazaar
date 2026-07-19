using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ByteBazaar.Api.Controllers;

[ApiController]
[Route("api/content")]
public class ContentController : ControllerBase
{
    private readonly BannerService _bannerService;

    public ContentController(BannerService bannerService)
    {
        _bannerService = bannerService;
    }

    /// <summary>Active banners within their scheduling window (Hero + Strip placements).</summary>
    [HttpGet("banners")]
    public async Task<ActionResult<List<BannerDto>>> GetBanners(CancellationToken ct)
        => Ok(await _bannerService.GetActiveBannersAsync(ct));
}
