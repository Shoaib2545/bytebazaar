using System.Text;
using System.Text.Json.Serialization;
using ByteBazaar.Api;
using ByteBazaar.Api.Middleware;
using ByteBazaar.Api.Services;
using ByteBazaar.Application.Abstractions;
using Hangfire;
using ByteBazaar.Application;
using ByteBazaar.Infrastructure;
using ByteBazaar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddScoped<JwtTokenService>();

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

    builder.Services.AddCors(options => options.AddPolicy("Frontends", policy => policy
        .WithOrigins("http://localhost:3000", "http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

    var jwt = builder.Configuration.GetSection("Jwt");
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
                ValidIssuer = jwt["Issuer"],
                ValidAudience = jwt["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
    builder.Services.AddAuthorization();

    var app = builder.Build();

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseCors("Frontends");
    app.UseAuthentication();
    app.UseAuthorization();
    // After authentication so the policies can skip caching authenticated requests.
    app.UseOutputCache();

    // Hangfire is registered only when the DB was reachable at startup (see AddInfrastructure).
    if (app.Environment.IsDevelopment() && app.Services.GetService<Hangfire.JobStorage>() is not null)
        app.UseHangfireDashboard("/hangfire");

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
            Log.Warning(ex, "Database is not reachable; skipping migration and seeding. The API will start anyway.");
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
