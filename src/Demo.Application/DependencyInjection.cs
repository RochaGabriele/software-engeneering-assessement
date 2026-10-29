using Demo.Application.Common;
using Demo.Application.Modules.ExchangeRates;
using Demo.Application.Modules.ExchangeRates.Contracts;
using Demo.Application.Modules.ExchangeRates.IsSynchronizationDue;
using Demo.Application.Modules.ExchangeRates.SynchronizeExchangeRates;
using Demo.Application.Modules.Inventory;
using Demo.Application.Modules.Inventory.GetProductBySku;
using Demo.Application.Modules.Inventory.GetProductCatalog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Demo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IQueryHandler<GetProductBySkuQuery, Result<ProductDto>>, GetProductBySkuQueryHandler>();
        services.AddScoped<IQueryHandler<GetProductCatalogQuery, Result<IReadOnlyList<ProductDto>>>, GetProductCatalogQueryHandler>();

        AddExchangeRates(services, configuration);

        return services;
    }

    private static void AddExchangeRates(IServiceCollection services, IConfiguration configuration)
    {
        // A misconfigured currency list fails startup rather than the first sync or request.
        services.AddOptions<ExchangeRatesOptions>()
            .Bind(configuration.GetSection(ExchangeRatesOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<ExchangeRatesOptions>, ExchangeRatesOptionsValidator>();
        services.AddSingleton(serviceProvider =>
            SupportedCurrencies.From(serviceProvider.GetRequiredService<IOptions<ExchangeRatesOptions>>().Value));

        services.AddScoped<ICurrencyConversionReader, CurrencyConversionReader>();
        services.AddScoped<ICommandHandler<SynchronizeExchangeRatesCommand, Result<SynchronizationReport>>, SynchronizeExchangeRatesCommandHandler>();
        services.AddScoped<IQueryHandler<IsSynchronizationDueQuery, bool>, IsSynchronizationDueQueryHandler>();
    }
}
