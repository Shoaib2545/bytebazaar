using ByteBazaar.Application.DTOs;
using ByteBazaar.Application.Exceptions;
using ByteBazaar.Infrastructure.Identity;
using ByteBazaar.Infrastructure.Persistence;
using ByteBazaar.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ByteBazaar.Tests;

public class StaffServiceTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private StaffService _service = null!;
    private UserManager<AppUser> _userManager = null!;
    private AppDbContext _db = null!;

    private AppUser _admin = null!;
    private AppUser _staff = null!;
    private AppUser _customer = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase($"bytebazaar-staff-{Guid.NewGuid()}"));
        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        _provider = services.BuildServiceProvider();
        _db = _provider.GetRequiredService<AppDbContext>();
        _userManager = _provider.GetRequiredService<UserManager<AppUser>>();

        var roleManager = _provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { "Admin", "Staff", "Customer" })
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));

        _admin = await CreateUserAsync("admin@test.local", "Primary Admin", "Admin");
        _staff = await CreateUserAsync("staff@test.local", "Staff Member", "Staff");
        _customer = await CreateUserAsync("customer@test.local", "Some Customer", "Customer");

        _service = new StaffService(_userManager, _db);
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    private async Task<AppUser> CreateUserAsync(string email, string fullName, string role, bool isActive = true)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            IsActive = isActive
        };
        var created = await _userManager.CreateAsync(user, "Password1!");
        Assert.True(created.Succeeded, string.Join(",", created.Errors.Select(e => e.Description)));
        await _userManager.AddToRoleAsync(user, role);
        return user;
    }

    [Fact]
    public async Task GetStaff_ReturnsAdminAndStaffOnly_WithRoles()
    {
        var staff = await _service.GetStaffAsync();

        Assert.Equal(2, staff.Count);
        Assert.DoesNotContain(staff, s => s.Email == "customer@test.local");
        Assert.Equal("Admin", staff.Single(s => s.Email == "admin@test.local").Role);
        Assert.Equal("Staff", staff.Single(s => s.Email == "staff@test.local").Role);
        Assert.All(staff, s => Assert.True(s.IsActive));
    }

    [Fact]
    public async Task Create_AddsStaffUserWithRole()
    {
        var dto = await _service.CreateAsync(new StaffCreateRequest
        {
            Email = "new@test.local",
            FullName = "New Staffer",
            Password = "Password1!",
            Role = "Staff"
        });

        Assert.Equal("Staff", dto.Role);
        Assert.True(dto.IsActive);

        var user = await _userManager.FindByEmailAsync("new@test.local");
        Assert.NotNull(user);
        Assert.True(await _userManager.IsInRoleAsync(user!, "Staff"));
        Assert.True(await _userManager.CheckPasswordAsync(user!, "Password1!"));
    }

    [Fact]
    public async Task Create_DuplicateEmail_Rejected()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateAsync(new StaffCreateRequest
        {
            Email = "staff@test.local",
            FullName = "Dup",
            Password = "Password1!",
            Role = "Staff"
        }));
    }

    [Fact]
    public async Task Create_UnknownRole_Rejected()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => _service.CreateAsync(new StaffCreateRequest
        {
            Email = "x@test.local",
            FullName = "X",
            Password = "Password1!",
            Role = "Customer"
        }));
    }

    [Fact]
    public async Task Update_LastActiveAdmin_CannotBeDemoted()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => _service.UpdateAsync(_admin.Id, new StaffUpdateRequest
        {
            FullName = _admin.FullName,
            Role = "Staff",
            IsActive = true
        }));
        Assert.True(await _userManager.IsInRoleAsync(_admin, "Admin"));
    }

    [Fact]
    public async Task Update_LastActiveAdmin_CannotBeDeactivated()
    {
        await Assert.ThrowsAsync<BadRequestException>(() => _service.UpdateAsync(_admin.Id, new StaffUpdateRequest
        {
            FullName = _admin.FullName,
            Role = "Admin",
            IsActive = false
        }));
        Assert.True((await _userManager.FindByIdAsync(_admin.Id.ToString()))!.IsActive);
    }

    [Fact]
    public async Task Update_AdminWithAnotherActiveAdmin_CanBeDemoted()
    {
        await CreateUserAsync("admin2@test.local", "Second Admin", "Admin");

        var dto = await _service.UpdateAsync(_admin.Id, new StaffUpdateRequest
        {
            FullName = "Demoted Admin",
            Role = "Staff",
            IsActive = true
        });

        Assert.NotNull(dto);
        Assert.Equal("Staff", dto!.Role);
        Assert.False(await _userManager.IsInRoleAsync(_admin, "Admin"));
        Assert.True(await _userManager.IsInRoleAsync(_admin, "Staff"));
    }

    [Fact]
    public async Task Update_InactiveOtherAdmin_DoesNotCountTowardsGuard()
    {
        await CreateUserAsync("admin3@test.local", "Inactive Admin", "Admin", isActive: false);

        await Assert.ThrowsAsync<BadRequestException>(() => _service.UpdateAsync(_admin.Id, new StaffUpdateRequest
        {
            FullName = _admin.FullName,
            Role = "Staff",
            IsActive = true
        }));
    }

    [Fact]
    public async Task Update_PromoteStaffToAdmin_Works()
    {
        var dto = await _service.UpdateAsync(_staff.Id, new StaffUpdateRequest
        {
            FullName = "Promoted",
            Role = "Admin",
            IsActive = true
        });

        Assert.Equal("Admin", dto!.Role);
        Assert.True(await _userManager.IsInRoleAsync(_staff, "Admin"));
        Assert.False(await _userManager.IsInRoleAsync(_staff, "Staff"));
        Assert.Equal("Promoted", (await _userManager.FindByIdAsync(_staff.Id.ToString()))!.FullName);
    }

    [Fact]
    public async Task Update_DeactivateStaff_Works()
    {
        var dto = await _service.UpdateAsync(_staff.Id, new StaffUpdateRequest
        {
            FullName = _staff.FullName,
            Role = "Staff",
            IsActive = false
        });

        Assert.False(dto!.IsActive);
        Assert.False((await _userManager.FindByIdAsync(_staff.Id.ToString()))!.IsActive);
    }

    [Fact]
    public async Task Update_CustomerOrUnknownUser_ReturnsNull()
    {
        var request = new StaffUpdateRequest { FullName = "X", Role = "Staff", IsActive = true };
        Assert.Null(await _service.UpdateAsync(_customer.Id, request));
        Assert.Null(await _service.UpdateAsync(Guid.NewGuid(), request));
    }

    [Fact]
    public async Task ResetPassword_ChangesPassword()
    {
        var ok = await _service.ResetPasswordAsync(_staff.Id, "NewPassword1!");

        Assert.True(ok);
        var user = await _userManager.FindByIdAsync(_staff.Id.ToString());
        Assert.True(await _userManager.CheckPasswordAsync(user!, "NewPassword1!"));
        Assert.False(await _userManager.CheckPasswordAsync(user!, "Password1!"));
    }

    [Fact]
    public async Task ResetPassword_CustomerOrUnknown_ReturnsFalse()
    {
        Assert.False(await _service.ResetPasswordAsync(_customer.Id, "NewPassword1!"));
        Assert.False(await _service.ResetPasswordAsync(Guid.NewGuid(), "NewPassword1!"));
    }
}
