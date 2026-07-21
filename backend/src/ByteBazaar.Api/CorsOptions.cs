namespace ByteBazaar.Api;

/// <summary>
/// Origins allowed by the "Frontends" CORS policy, driven from configuration
/// (<c>Cors:AllowedOrigins:0</c>, or <c>Cors__AllowedOrigins__0</c> as an env var) so a real
/// deployment does not need a code change to let its own storefront and admin panel talk to it.
///
/// The policy allows credentials, which makes wildcards illegal by spec — the browser refuses
/// <c>Access-Control-Allow-Origin: *</c> together with credentials. So this is always an explicit
/// list, and an empty list in a non-Development environment is a boot failure rather than a
/// silently CORS-blocked frontend.
/// </summary>
public class FrontendCorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "Frontends";

    /// <summary>Local dev origins, applied only when the configured list is empty in Development.</summary>
    public static readonly string[] DevelopmentDefaults =
    {
        "http://localhost:3000", // storefront (Next.js)
        "http://localhost:5173"  // admin (Vite)
    };

    public List<string> AllowedOrigins { get; set; } = new();

    public string[] Resolve(bool isDevelopment)
    {
        var origins = AllowedOrigins
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (origins.Length > 0)
        {
            foreach (var origin in origins)
            {
                if (origin == "*")
                {
                    throw new InvalidOperationException(
                        "Cors:AllowedOrigins cannot contain \"*\": the Frontends policy allows credentials, " +
                        "and browsers reject a wildcard origin on credentialed requests. List real origins.");
                }

                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new InvalidOperationException(
                        $"Cors:AllowedOrigins contains \"{origin}\", which is not an absolute http(s) origin.");
                }
            }

            return origins;
        }

        if (isDevelopment) return DevelopmentDefaults;

        throw new InvalidOperationException(
            "Cors:AllowedOrigins is empty. Set Cors__AllowedOrigins__0 / __1 to the storefront and admin " +
            "origins; without them both frontends are CORS-blocked.");
    }
}
