using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Application.Services;

/// <summary>
/// Admin-managed URL redirects. Paths are normalized on write and on lookup so
/// "/Old-Page/", "/old-page" and "old-page" are the same rule.
/// </summary>
public class RedirectService
{
    private readonly IAppDbContext _db;

    public RedirectService(IAppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lowercases, drops any query/fragment, forces a single leading slash and trims the trailing
    /// slash (the site root stays "/"). Returns "/" for null/blank input.
    /// </summary>
    public static string NormalizePath(string? path)
    {
        var value = (path ?? string.Empty).Trim();
        var cut = value.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0) value = value[..cut];

        value = value.Trim().TrimEnd('/');
        if (value.Length == 0) return "/";
        if (!value.StartsWith('/')) value = "/" + value;
        return value.ToLowerInvariant();
    }

    // ----- Public lookup -----

    /// <summary>Returns the active redirect for <paramref name="path"/>, or null when there is none.</summary>
    public async Task<RedirectLookupDto?> LookupAsync(string? path, CancellationToken ct = default)
    {
        var from = NormalizePath(path);
        var redirect = await _db.Redirects.AsNoTracking()
            .FirstOrDefaultAsync(r => r.IsActive && r.FromPath == from, ct);
        if (redirect is null) return null;

        return new RedirectLookupDto
        {
            ToPath = redirect.ToPath,
            IsPermanent = redirect.IsPermanent,
            StatusCode = redirect.IsPermanent ? 301 : 302
        };
    }

    /// <summary>All active redirects — lets the storefront preload the (small) rule set.</summary>
    public async Task<List<AdminRedirectDto>> GetActiveAsync(CancellationToken ct = default)
    {
        var redirects = await _db.Redirects.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.FromPath)
            .ToListAsync(ct);
        return redirects.Select(ToDto).ToList();
    }

    // ----- Admin CRUD -----

    public async Task<List<AdminRedirectDto>> GetAllAsync(CancellationToken ct = default)
    {
        var redirects = await _db.Redirects.AsNoTracking()
            .OrderBy(r => r.FromPath)
            .ToListAsync(ct);
        return redirects.Select(ToDto).ToList();
    }

    public async Task<AdminRedirectDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var redirect = await _db.Redirects.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return redirect is null ? null : ToDto(redirect);
    }

    /// <summary>Returns null when another rule already maps the same normalized FromPath.</summary>
    public async Task<AdminRedirectDto?> CreateAsync(RedirectUpsertRequest request, CancellationToken ct = default)
    {
        var from = NormalizePath(request.FromPath);
        if (await _db.Redirects.AnyAsync(r => r.FromPath == from, ct)) return null;

        var redirect = new Redirect { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        Apply(redirect, request);
        _db.Redirects.Add(redirect);
        await _db.SaveChangesAsync(ct);
        return ToDto(redirect);
    }

    /// <summary>Returns (found, dto). dto is null with found=true when the new FromPath collides.</summary>
    public async Task<(bool Found, AdminRedirectDto? Dto)> UpdateAsync(
        Guid id, RedirectUpsertRequest request, CancellationToken ct = default)
    {
        var redirect = await _db.Redirects.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (redirect is null) return (false, null);

        var from = NormalizePath(request.FromPath);
        if (await _db.Redirects.AnyAsync(r => r.Id != id && r.FromPath == from, ct)) return (true, null);

        Apply(redirect, request);
        await _db.SaveChangesAsync(ct);
        return (true, ToDto(redirect));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var redirect = await _db.Redirects.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (redirect is null) return false;
        _db.Redirects.Remove(redirect);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void Apply(Redirect redirect, RedirectUpsertRequest request)
    {
        redirect.FromPath = NormalizePath(request.FromPath);
        redirect.ToPath = request.ToPath.Trim();
        redirect.IsPermanent = request.IsPermanent;
        redirect.IsActive = request.IsActive;
    }

    private static AdminRedirectDto ToDto(Redirect r) => new()
    {
        Id = r.Id,
        FromPath = r.FromPath,
        ToPath = r.ToPath,
        IsPermanent = r.IsPermanent,
        IsActive = r.IsActive,
        CreatedAt = r.CreatedAt
    };
}
