using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebBuilders.Domains.Business.Services;

namespace WebBuilders.Domains.Business;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PricingOptions>(configuration.GetSection(PricingOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPricingService, PricingService>();
        services.AddScoped<IDomainSearchService, DomainSearchService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IOrderService, OrderService>();

        return services;
    }
}
