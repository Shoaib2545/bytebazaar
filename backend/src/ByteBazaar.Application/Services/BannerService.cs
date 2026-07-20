using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

public class BannerService
{
    /// <summary>Short TTL: banners are scheduled, so a cached list must expire near its window edges.</summary>
    private static readonly TimeSpan ActiveBannersTtl = TimeSpan.FromMinutes(2);

    private readonly IAppDbContext _db;
    private readonly ICacheStore _cache;

    /// <summary>Uncached construction (tests); production uses the ICacheStore overload.</summary>
    public BannerService(IAppDbContext db) : this(db, NoOpCacheStore.Instance)
    {
    }

    public BannerService(IAppDbContext db, ICacheStore cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <summary>
    /// Public storefront banners: active and within their [StartsAt, EndsAt] window. Homepage hot
    /// data — cached in Redis and evicted by every admin banner write.
    /// </summary>
    public Task<List<BannerDto>> GetActiveBannersAsync(CancellationToken ct = default)
        => _cache.GetOrSetAsync(CacheKeys.HomeBanners, LoadActiveBannersAsync, ActiveBannersTtl, ct);

    private async Task<List<BannerDto>> LoadActiveBannersAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await _db.Banners.AsNoTracking()
            .Where(b => b.IsActive
                        && (b.StartsAt == null || b.StartsAt <= now)
                        && (b.EndsAt == null || b.EndsAt >= now))
            .OrderBy(b => b.Placement).ThenBy(b => b.SortOrder).ThenBy(b => b.Title)
            .Select(b => new BannerDto
            {
                Id = b.Id,
                Title = b.Title,
                Subtitle = b.Subtitle,
                ImageUrl = b.ImageUrl,
                LinkUrl = b.LinkUrl,
                Placement = b.Placement,
                SortOrder = b.SortOrder
            })
            .ToListAsync(ct);
    }

    // ----- Admin CRUD -----

    public async Task<List<AdminBannerDto>> GetBannersAsync(CancellationToken ct = default)
    {
        var banners = await _db.Banners.AsNoTracking()
            .OrderBy(b => b.Placement).ThenBy(b => b.SortOrder).ThenBy(b => b.Title)
            .ToListAsync(ct);
        return banners.Select(ToDto).ToList();
    }

    public async Task<AdminBannerDto> CreateAsync(BannerUpsertRequest request, CancellationToken ct = default)
    {
        var banner = new Banner { Id = Guid.NewGuid() };
        Apply(banner, request);
        _db.Banners.Add(banner);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.HomeBanners, ct);
        return ToDto(banner);
    }

    public async Task<AdminBannerDto?> UpdateAsync(Guid id, BannerUpsertRequest request, CancellationToken ct = default)
    {
        var banner = await _db.Banners.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (banner is null) return null;
        Apply(banner, request);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.HomeBanners, ct);
        return ToDto(banner);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var banner = await _db.Banners.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (banner is null) return false;
        _db.Banners.Remove(banner);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CacheKeys.HomeBanners, ct);
        return true;
    }

    private static void Apply(Banner banner, BannerUpsertRequest request)
    {
        banner.Title = request.Title;
        banner.Subtitle = request.Subtitle;
        banner.ImageUrl = request.ImageUrl;
        banner.LinkUrl = request.LinkUrl;
        banner.Placement = request.Placement;
        banner.SortOrder = request.SortOrder;
        banner.IsActive = request.IsActive;
        banner.StartsAt = request.StartsAt;
        banner.EndsAt = request.EndsAt;
    }

    private static AdminBannerDto ToDto(Banner b) => new()
    {
        Id = b.Id,
        Title = b.Title,
        Subtitle = b.Subtitle,
        ImageUrl = b.ImageUrl,
        LinkUrl = b.LinkUrl,
        Placement = b.Placement,
        SortOrder = b.SortOrder,
        IsActive = b.IsActive,
        StartsAt = b.StartsAt,
        EndsAt = b.EndsAt
    };
}
