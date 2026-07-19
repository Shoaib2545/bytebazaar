using ByteBazaar.Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ByteBazaar.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CatalogService>();
        services.AddScoped<AdminCatalogService>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
