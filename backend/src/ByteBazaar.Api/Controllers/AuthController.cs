using ByteBazaar.Api.Services;
using ByteBazaar.Application.Abstractions;
using ByteBazaar.Application.DTOs;
using ByteBazaar.Domain.Entities;
using ByteBazaar.Infrastructure.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ByteBazaar.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string RefreshCookieName = "bb_refresh";
    private const int RefreshTokenDays = 14;

    private readonly UserManager<AppUser> _userManager;
    private readonly IAppDbContext _db;
    private readonly JwtTokenService _tokenService;

    public AuthController(UserManager<AppUser> userManager, IAppDbContext db, JwtTokenService tokenService)
    {
        _userManager = userManager;
        _db = db;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request, [FromServices] IValidator<RegisterRequest> validator)
    {
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid)
            return ValidationProblem(ToModelState(validation));

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            Phone = request.Phone
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code, error.Description);
            return ValidationProblem(ModelState);
        }

        await _userManager.AddToRoleAsync(user, "Customer");
        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request, [FromServices] IValidator<LoginRequest> validator)
    {
        var validation = await validator.ValidateAsync(request);
        if (!validation.IsValid)
            return ValidationProblem(ToModelState(validation));

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Invalid credentials" });

        if (!user.IsActive)
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Account deactivated" });

        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var tokenValue = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(tokenValue))
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Missing refresh token" });

        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == tokenValue);
        if (token is null || token.RevokedAt is not null || token.ExpiresAt <= DateTime.UtcNow)
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Invalid refresh token" });

        var user = await _userManager.FindByIdAsync(token.UserId.ToString());
        if (user is null || !user.IsActive)
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Invalid refresh token" });

        token.RevokedAt = DateTime.UtcNow;
        return Ok(await IssueTokensAsync(user));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var tokenValue = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(tokenValue))
        {
            var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == tokenValue);
            if (token is not null && token.RevokedAt is null)
            {
                token.RevokedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }

        Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions(DateTimeOffset.UtcNow));
        return NoContent();
    }

    private async Task<AuthResponseDto> IssueTokensAsync(AppUser user)
    {
        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        var accessToken = _tokenService.CreateAccessToken(user, roles);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = JwtTokenService.CreateRefreshTokenValue(),
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenDays)
        };
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        Response.Cookies.Append(RefreshCookieName, refreshToken.Token,
            BuildCookieOptions(DateTimeOffset.UtcNow.AddDays(RefreshTokenDays)));

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Roles = roles
            }
        };
    }

    private CookieOptions BuildCookieOptions(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expires
    };

    private Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary ToModelState(
        FluentValidation.Results.ValidationResult validation)
    {
        foreach (var error in validation.Errors)
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        return ModelState;
    }
}
