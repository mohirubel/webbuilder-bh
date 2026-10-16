using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebBuilders.Domains.Business.Abstractions;
using WebBuilders.Domains.Data.Payments;
using WebBuilders.Domains.Data.Registrars;
using WebBuilders.Domains.Data.Repositories;

namespace WebBuilders.Domains.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddDataLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NamecheapOptions>(configuration.GetSection(NamecheapOptions.SectionName));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.Configure<RdapOptions>(configuration.GetSection(RdapOptions.SectionName));

        // Strategy: use the Namecheap sandbox when credentials exist, otherwise fall back to RDAP lookups.
        var namecheap = configuration.GetSection(NamecheapOptions.SectionName).Get<NamecheapOptions>() ?? new();
        if (namecheap.IsConfigured)
        {
            services.AddHttpClient<IDomainRegistrar, NamecheapRegistrar>(c => c.Timeout = TimeSpan.FromSeconds(30));
        }
        else
        {
            services.AddMemoryCache();
            services.AddHttpClient<IDomainRegistrar, RdapRegistrar>(c =>
            {
                c.Timeout = TimeSpan.FromSeconds(8);
                c.DefaultRequestHeaders.Accept.ParseAdd("application/rdap+json, application/json");
                c.DefaultRequestHeaders.UserAgent.ParseAdd("WebBuilders-Domains/1.0"); // some registries reject anonymous clients
            });
        }

        services.AddHttpClient<IPaymentGateway, StripePaymentGateway>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

        return services;
    }
}
