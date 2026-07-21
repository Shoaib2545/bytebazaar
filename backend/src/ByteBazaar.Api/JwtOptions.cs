using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ByteBazaar.Api;

/// <summary>
/// Strongly-typed JWT configuration, validated once at startup so a misconfigured signing key is a
/// loud boot failure rather than a silent security hole discovered in production.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// The key shipped in appsettings.json. It is in source control and therefore public: anyone
    /// who has read the repository can mint an admin token with it. Booting a non-Development
    /// environment with this value is refused outright.
    /// </summary>
    public const string DevelopmentKey = "bytebazaar-dev-signing-key-please-change-in-production-0123456789";

    /// <summary>HMAC-SHA256 needs at least 256 bits of key material; anything shorter is rejected
    /// by the token handler at *signing* time, i.e. on the first login rather than at boot.</summary>
    private const int MinimumKeyBytes = 32;

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "ByteBazaar";
    public string Audience { get; set; } = "ByteBazaar.Clients";

    /// <summary>Short by design: revocation is handled by rotating refresh tokens, so the access
    /// token's blast radius is bounded by its lifetime.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Refresh-token lifetime. Rotated on every use and revoked on logout.</summary>
    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>Tolerance for clock drift between issuer and validator. The framework default is
    /// five minutes, which silently extends every token's life by 5 min; 30 s is plenty.</summary>
    public int ClockSkewSeconds { get; set; } = 30;

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));

    /// <summary>Throws on any configuration that would be unsafe or non-functional in production.</summary>
    public void Validate(bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(Key))
            throw new InvalidOperationException("Jwt:Key is not configured. Set Jwt__Key (>=64 random chars).");

        if (Encoding.UTF8.GetByteCount(Key) < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:Key must be at least {MinimumKeyBytes} bytes for HMAC-SHA256; got {Encoding.UTF8.GetByteCount(Key)}.");
        }

        if (!isDevelopment && Key == DevelopmentKey)
        {
            throw new InvalidOperationException(
                "Jwt:Key is still the published development key from appsettings.json. " +
                "Generate a fresh one (openssl rand -base64 64) and set Jwt__Key before starting a non-Development environment.");
        }

        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must both be configured.");

        if (AccessTokenMinutes is < 1 or > 120)
            throw new InvalidOperationException("Jwt:AccessTokenMinutes must be between 1 and 120.");

        if (RefreshTokenDays is < 1 or > 90)
            throw new InvalidOperationException("Jwt:RefreshTokenDays must be between 1 and 90.");
    }
}
