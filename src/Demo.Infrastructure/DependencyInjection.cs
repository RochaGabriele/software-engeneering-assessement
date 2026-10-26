using Demo.Application.Common;
using Demo.Application.Modules.ExchangeRates.Ports;
using Demo.Domain.Common.Services;
using Demo.Domain.Modules.ExchangeRates;
using Demo.Domain.Modules.Inventory;
using Demo.Infrastructure.ExchangeRates.OpenExchangeRates;
using Demo.Infrastructure.Messaging;
using Demo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Demo.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Demo";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // The connection string is resolved when the context is built, not at registration time,
        // so hosts that override configuration later (e.g. integration tests) are honored.
        services.AddDbContext<DemoDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

            options.UseSqlite(connectionString).UseDemoSeeding();
        });

        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<DemoDbContext>());
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IExchangeRateRepository, ExchangeRateRepository>();
        services.AddScoped<IExchangeRateHistoryRepository, ExchangeRateHistoryRepository>();
        services.AddScoped<IMessagePublisher, FakeMessagePublisher>();

        AddOpenExchangeRates(services, configuration);

        services.AddInfrastructureHangfire();

        return services;
    }

    private static void AddOpenExchangeRates(IServiceCollection services, IConfiguration configuration)
    {
        // AppId is deliberately not validated here: without it the API still serves the USD catalog,
        // and only the sync fails, with an explicit message (ADR-0007).
        services.AddOptions<OpenExchangeRatesOptions>()
            .Bind(configuration.GetSection(OpenExchangeRatesOptions.SectionName))
            .Validate(
                options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _) && options.BaseUrl.EndsWith('/'),
                $"{OpenExchangeRatesOptions.SectionName}:{nameof(OpenExchangeRatesOptions.BaseUrl)} must be an absolute URL ending with '/'.")
            .ValidateOnStart();

        // Standard pipeline: attempt and total timeouts, exponential retry with jitter on transient failures
        // (5xx, 408, 429, network), and a circuit breaker. 401/403 are not retried.
        services.AddHttpClient<IExchangeRateProvider, OpenExchangeRatesClient>((serviceProvider, client) =>
                client.BaseAddress = new Uri(serviceProvider.GetRequiredService<IOptions<OpenExchangeRatesOptions>>().Value.BaseUrl))
            .AddStandardResilienceHandler();
    }
}
