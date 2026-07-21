using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using ByteBazaar.Infrastructure.Identity;
using Microsoft.IdentityModel.Tokens;

namespace ByteBazaar.Api.Services;

public class JwtTokenService
{
    private readonly JwtOptions _options;

    // JwtOptions is validated once at startup (Program.cs), so nothing here has to defend against
    // a missing or too-short signing key.
    public JwtTokenService(JwtOptions options)
    {
        _options = options;
    }

    public int RefreshTokenDays => _options.RefreshTokenDays;

    public string CreateAccessToken(AppUser user, IEnumerable<string> roles)
    {
        var credentials = new SigningCredentials(_options.SigningKey(), SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            // Unique per token: lets a future revocation list identify an individual access token.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("fullName", user.FullName)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>512 bits from the CSPRNG — the refresh token is a bearer secret with a 14-day
    /// life, so it must not be guessable and must not encode anything.</summary>
    public static string CreateRefreshTokenValue()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }
}
