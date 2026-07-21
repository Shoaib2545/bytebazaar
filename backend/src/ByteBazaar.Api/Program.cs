using System.Text.Json;
using System.Text.Json.Serialization;
using ByteBazaar.Api;
using ByteBazaar.Api.Middleware;
using ByteBazaar.Api.Services;
using ByteBazaar.Application.Abstractions;
using Hangfire;
using ByteBazaar.Application;
using ByteBazaar.Infrastructure;
using ByteBazaar.Infrastructure.Health;
using ByteBazaar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    var isDevelopment = builder.Environment.IsDevelopment();

    builder.Host.UseSerilog((context, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddScoped<JwtTokenService>();

    // Liveness + readiness. Postgres is fatal to readiness; Redis and Meilisearch degrade.
    builder.Services.AddByteBazaarHealthChecks(builder.Configuration);

    // Output caching for the heavy catalog/filter endpoints; admin catalog writes evict by tag.
    builder.Services.AddOutputCache(CachePolicies.Configure);
    builder.Services.AddSingleton<IOutputCacheInvalidator, OutputCacheInvalidator>();

    builder.Services.AddControllers().AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "ByteBazaar API", Version = "v1" });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter the JWT access token."
        });
        options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
        });
    });

    // ---------------------------------------------------------------------- CORS
    var corsOptions = builder.Configuration.GetSection(FrontendCorsOptions.SectionName)
        .Get<FrontendCorsOptions>() ?? new FrontendCorsOptions();
    var allowedOrigins = corsOptions.Resolve(isDevelopment);
    Log.Information("CORS policy \"{Policy}\" allows {Origins}", FrontendCorsOptions.PolicyName, allowedOrigins);

    builder.Services.AddCors(options => options.AddPolicy(FrontendCorsOptions.PolicyName, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        // Both frontends send the bb_refresh cookie, so credentials must stay on.
        .AllowCredentials()));

    // ------------------------------------------------------------- rate limiting
    var rateLimits = builder.Configuration.GetSection(RateLimitOptions.SectionName)
        .Get<RateLimitOptions>() ?? new RateLimitOptions();
    builder.Services.AddSingleton(rateLimits);
    if (rateLimits.Enabled)
        builder.Services.AddRateLimiter(limiter => RateLimitPolicies.Configure(limiter, rateLimits));

    // Behind Caddy/nginx the peer address is the proxy, which would put every client in one
    // rate-limit partition. Enabled explicitly (Reverse Proxy:Enabled) so it is never on by
    // accident — trusting X-Forwarded-For without a proxy in front lets clients spoof their IP
    // and escape the limiter entirely.
    var trustForwardedHeaders = builder.Configuration.GetValue("ReverseProxy:Enabled", false);
    if (trustForwardedHeaders)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // The proxy runs on the compose network with a dynamic address, so the default
            // known-proxy allowlist cannot be used. Safe only because nothing but the proxy can
            // reach the API: it publishes no host port in docker-compose.prod.yml.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            options.ForwardLimit = builder.Configuration.GetValue("ReverseProxy:ForwardLimit", 1);
        });
    }

    // -------------------------------------------------------------------- auth
    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
    jwtOptions.Validate(isDevelopment);
    if (isDevelopment && jwtOptions.Key == JwtOptions.DevelopmentKey)
        Log.Warning("Jwt:Key is the published development key. This is refused outside Development.");
    builder.Services.AddSingleton(jwtOptions);

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                // Pin the algorithm: without this a token handler will accept any algorithm the
                // key type supports, which is the classic JWT confusion foothold.
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = jwtOptions.SigningKey(),
                ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds)
            };
        });
    builder.Services.AddAuthorization();

    var app = builder.Build();

    if (trustForwardedHeaders)
        app.UseForwardedHeaders();

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();

    // Swagger is a development affordance; in production the Caddyfile 404s it, but not
    // publishing it at all is the stronger guarantee.
    if (isDevelopment || app.Configuration.GetValue("Swagger:Enabled", false))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors(FrontendCorsOptions.PolicyName);
    app.UseAuthentication();
    app.UseAuthorization();
    // After authentication so the limiter can partition on the authenticated user id and the
    // cache policies can skip caching authenticated requests.
    if (rateLimits.Enabled)
        app.UseRateLimiter();
    app.UseOutputCache();

    // Hangfire is registered only when the DB was reachable at startup (see AddInfrastructure).
    if (app.Environment.IsDevelopment() && app.Services.GetService<Hangfire.JobStorage>() is not null)
        app.UseHangfireDashboard("/hangfire");

    // ------------------------------------------------------------------ health
    // /health/live — process only. Never fails on a datastore outage; a liveness probe that does
    // would restart-loop the API during a database incident.
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains(HealthCheckExtensions.LiveTag),
        ResponseWriter = WriteHealthResponse
    }).AllowAnonymous();

    // /health/ready — Postgres must be reachable and migrated; Redis and Meilisearch report
    // Degraded (still HTTP 200) because both have working fallbacks.
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains(HealthCheckExtensions.ReadyTag),
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        },
        ResponseWriter = WriteHealthResponse
    }).AllowAnonymous();

    // /health — everything, for humans. Same status semantics as readiness.
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        },
        ResponseWriter = WriteHealthResponse
    }).AllowAnonymous();

    app.MapControllers();

    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            await DbSeeder.SeedAsync(scope.ServiceProvider);
            Log.Information("Database migrated and seeded");
        }
        catch (Exception ex)
        {
            // Deliberate: a transient database blip at boot must not crash-loop the container.
            // /health/ready is what tells the orchestrator this instance cannot serve yet.
            Log.Warning(ex,
                "Database is not reachable; skipping migration and seeding. The API will start anyway " +
                "and /health/ready will report Unhealthy until the database recovers.");
        }

        try
        {
            // Best-effort: brings a fresh Meilisearch container in line with the seeded catalog.
            // Degrades to a no-op when Meilisearch is unconfigured or unreachable.
            await scope.ServiceProvider.GetRequiredService<ISearchIndexQueue>().EnqueueFullReindexAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not queue the startup search re-index. The API will start anyway.");
        }
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "ByteBazaar API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Per-check JSON so an operator can see *which* dependency is degraded without reading logs.
static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
            error = entry.Value.Exception?.Message
        })
    }, new JsonSerializerOptions { WriteIndented = true }));
}

/// <summary>Exposed so integration tests can drive the real pipeline via WebApplicationFactory.</summary>
public partial class Program;
