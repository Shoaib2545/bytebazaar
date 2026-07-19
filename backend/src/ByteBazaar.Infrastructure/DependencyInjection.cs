using ByteBazaar.Application.Abstractions;
using ByteBazaar.Infrastructure.Identity;
using ByteBazaar.Infrastructure.Notifications;
using ByteBazaar.Infrastructure.Persistence;
using ByteBazaar.Infrastructure.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ByteBazaar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.ConfigureDataSource(ds => ds.EnableDynamicJson())));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddHttpClient(RevalidationService.HttpClientName, client =>
            client.Timeout = TimeSpan.FromSeconds(3));
        services.AddSingleton<IStorefrontRevalidator, RevalidationService>();

        services.AddScoped<IOrderNotifier, SerilogOrderNotifier>();
        services.AddScoped<IOrderNotificationQueue, OrderNotificationQueue>();

        // Identity-backed admin services (query AspNet* tables directly).
        services.AddScoped<AdminCustomerService>();
        services.AddScoped<StaffService>();

        // Hangfire is wired only when the database is reachable at startup (same guard as
        // migrations in Program.cs). When absent, OrderNotificationQueue calls the notifier inline.
        if (CanConnect(connectionString))
        {
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));
            services.AddHangfireServer();
        }

        return services;
    }

    private static bool CanConnect(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString) { Timeout = 3 };
            using var connection = new NpgsqlConnection(builder.ConnectionString);
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
