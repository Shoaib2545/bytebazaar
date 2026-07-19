using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using ByteBazaar.Infrastructure.Identity;
using ByteBazaar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Infrastructure.Services;

/// <summary>
/// Staff account management (Admin + Staff users) via UserManager. Guards against demoting
/// or deactivating the last active Admin. Exposed only to the Admin role at the API layer.
/// </summary>
public class StaffService
{
    private const string AdminRole = "Admin";
    private const string StaffRole = "Staff";

    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _db;

    public StaffService(UserManager<AppUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<List<StaffUserDto>> GetStaffAsync(CancellationToken ct = default)
    {
        var rows = await (
                from u in _db.Users.AsNoTracking()
                join ur in _db.UserRoles on u.Id equals ur.UserId
                join r in _db.Roles on ur.RoleId equals r.Id
                where r.Name == AdminRole || r.Name == StaffRole
                select new { u.Id, u.Email, u.FullName, u.IsActive, Role = r.Name! })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.Id)
            .Select(g =>
            {
                var first = g.First();
                return new StaffUserDto
                {
                    Id = first.Id,
                    Email = first.Email ?? string.Empty,
                    FullName = first.FullName,
                    // A user in both roles is reported as Admin.
                    Role = g.Any(r => r.Role == AdminRole) ? AdminRole : StaffRole,
                    IsActive = first.IsActive
                };
            })
            .OrderBy(s => s.FullName).ThenBy(s => s.Email)
            .ToList();
    }

    public async Task<StaffUserDto> CreateAsync(StaffCreateRequest request, CancellationToken ct = default)
    {
        var role = NormalizeRole(request.Role);

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            FullName = request.FullName,
            IsActive = true
        };

        ThrowIfFailed(await _userManager.CreateAsync(user, request.Password));
        ThrowIfFailed(await _userManager.AddToRoleAsync(user, role));

        return ToDto(user, role);
    }

    public async Task<StaffUserDto?> UpdateAsync(Guid id, StaffUpdateRequest request, CancellationToken ct = default)
    {
        var role = NormalizeRole(request.Role);

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return null;

        var isAdmin = await _userManager.IsInRoleAsync(user, AdminRole);
        var isStaff = await _userManager.IsInRoleAsync(user, StaffRole);
        if (!isAdmin && !isStaff) return null; // not a staff account

        // Last-active-admin guard: an active Admin may not be demoted or deactivated
        // unless another active Admin remains.
        var losesAdminPowers = isAdmin && user.IsActive && (role != AdminRole || !request.IsActive);
        if (losesAdminPowers && await CountOtherActiveAdminsAsync(user.Id, ct) == 0)
            throw new BadRequestException("Cannot demote or deactivate the last active admin.");

        user.FullName = request.FullName;
        user.IsActive = request.IsActive;
        ThrowIfFailed(await _userManager.UpdateAsync(user));

        if (isAdmin && role != AdminRole)
            ThrowIfFailed(await _userManager.RemoveFromRoleAsync(user, AdminRole));
        if (isStaff && role != StaffRole)
            ThrowIfFailed(await _userManager.RemoveFromRoleAsync(user, StaffRole));
        if (!await _userManager.IsInRoleAsync(user, role))
            ThrowIfFailed(await _userManager.AddToRoleAsync(user, role));

        return ToDto(user, role);
    }

    public async Task<bool> ResetPasswordAsync(Guid id, string newPassword, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null) return false;

        var isStaffAccount = await _userManager.IsInRoleAsync(user, AdminRole)
                             || await _userManager.IsInRoleAsync(user, StaffRole);
        if (!isStaffAccount) return false;

        if (await _userManager.HasPasswordAsync(user))
            ThrowIfFailed(await _userManager.RemovePasswordAsync(user));
        ThrowIfFailed(await _userManager.AddPasswordAsync(user, newPassword));
        return true;
    }

    private async Task<int> CountOtherActiveAdminsAsync(Guid excludeUserId, CancellationToken ct)
    {
        return await (
                from u in _db.Users.AsNoTracking()
                join ur in _db.UserRoles on u.Id equals ur.UserId
                join r in _db.Roles on ur.RoleId equals r.Id
                where r.Name == AdminRole && u.IsActive && u.Id != excludeUserId
                select u.Id)
            .CountAsync(ct);
    }

    private static string NormalizeRole(string role) => role switch
    {
        AdminRole => AdminRole,
        StaffRole => StaffRole,
        _ => throw new BadRequestException("Role must be \"Admin\" or \"Staff\".")
    };

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new BadRequestException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    private static StaffUserDto ToDto(AppUser user, string role) => new()
    {
        Id = user.Id,
        Email = user.Email ?? string.Empty,
        FullName = user.FullName,
        Role = role,
        IsActive = user.IsActive
    };
}
