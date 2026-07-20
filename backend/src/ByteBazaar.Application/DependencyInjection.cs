using ByteBazaar.Application.Abstractions;
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
        services.AddScoped<CartService>();
        services.AddScoped<CouponService>();
        services.AddScoped<AdminCouponService>();
        services.AddScoped<BannerService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<OrderService>();
        services.AddScoped<AdminOrderService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ReportService>();
        services.AddScoped<WishlistService>();
        services.AddScoped<AddressService>();
        services.AddScoped<SearchService>();
        services.AddScoped<SearchIndexingService>();
        services.AddScoped<RedirectService>();
        services.AddSingleton<IShippingOptionsProvider, DefaultShippingOptionsProvider>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
