using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ByteBazaar.Api;

/// <summary>
/// One fixed-window budget. Deliberately not a token bucket: fixed windows are trivial to reason
/// about from an operations desk ("10 login attempts per minute per IP") and to explain in a 429.
/// </summary>
public class RateLimitWindowOptions
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; } = 60;

    /// <summary>Requests queued instead of rejected. 0 = reject immediately, which is what we want
    /// for abuse control: queueing an attacker's requests just holds server threads.</summary>
    public int QueueLimit { get; set; }
}

/// <summary>
/// Bound from the "RateLimiting" configuration section, so limits are tunable per environment
/// (appsettings, or <c>RateLimiting__Auth__PermitLimit</c> as an env var) without a redeploy of code.
/// </summary>
public class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Kill switch. Set false for load testing or a local debugging session.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Everything not covered by a named policy.</summary>
    public RateLimitWindowOptions Global { get; set; } = new() { PermitLimit = 300, WindowSeconds = 60 };

    /// <summary>Brute-force surface: login / register / refresh.</summary>
    public RateLimitWindowOptions Auth { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    /// <summary>Order placement — cheap to send, expensive to process and clean up.</summary>
    public RateLimitWindowOptions Checkout { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
}

public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Checkout = "checkout";

    /// <summary>
    /// Paths exempted from the global limiter. Health probes run on a fixed interval from the
    /// orchestrator and must never be throttled — a 429 there reads as "unhealthy" and would
    /// trigger a restart loop under exactly the load the limiter exists to survive.
    /// </summary>
    private static bool IsExempt(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Partition key: the authenticated user id when there is one, the client IP otherwise.
    /// Using the user id for signed-in traffic means a shared NAT/office IP does not put all of
    /// its users in one bucket, and a single compromised account cannot hide behind IP rotation.
    /// </summary>
    private static string PartitionKey(HttpContext context)
    {
        var userId = context.User.GetUserId();
        if (userId is not null) return "u:" + userId.Value.ToString("N");

        // RemoteIpAddress is only trustworthy once ForwardedHeaders has run (see Program.cs);
        // without a proxy it is already the real peer.
        var ip = context.Connection.RemoteIpAddress;
        return "ip:" + (ip is null ? "unknown" : ip.ToString());
    }

    private static RateLimitPartition<string> Window(HttpContext context, RateLimitWindowOptions options) =>
        RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, options.PermitLimit),
            Window = TimeSpan.FromSeconds(Math.Max(1, options.WindowSeconds)),
            QueueLimit = Math.Max(0, options.QueueLimit),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });

    public static void Configure(RateLimiterOptions limiter, RateLimitOptions options)
    {
        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            IsExempt(context)
                ? RateLimitPartition.GetNoLimiter("health")
                : Window(context, options.Global));

        limiter.AddPolicy(Auth, context => Window(context, options.Auth));
        limiter.AddPolicy(Checkout, context => Window(context, options.Checkout));

        limiter.OnRejected = async (context, ct) =>
        {
            // Retry-After is the whole point of a well-behaved 429: it tells a legitimate client
            // (and our own admin SPA) exactly how long to back off instead of hot-looping.
            var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var window)
                ? (int)Math.Ceiling(window.TotalSeconds)
                : options.Global.WindowSeconds;

            context.HttpContext.Response.Headers.RetryAfter =
                retryAfter.ToString(NumberFormatInfo.InvariantInfo);
            context.HttpContext.Response.ContentType = "application/problem+json";

            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc6585#section-4",
                title = "Too many requests",
                status = StatusCodes.Status429TooManyRequests,
                detail = $"Rate limit exceeded. Retry after {retryAfter} second(s).",
                retryAfter
            }, ct);
        };
    }
}
