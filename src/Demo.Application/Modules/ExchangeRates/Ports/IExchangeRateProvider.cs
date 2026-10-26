using Demo.Domain.Common;

namespace Demo.Application.Modules.ExchangeRates.Ports;

/// <summary>Outbound port to an exchange-rate source. Adapters translate the provider's model into these types (ADR-0007).</summary>
public interface IExchangeRateProvider
{
    /// <exception cref="ExchangeRateProviderException">The provider could not be reached or answered with an error.</exception>
    Task<LatestRates> GetLatestRatesAsync(Currency baseCurrency, IReadOnlyCollection<Currency> targets, CancellationToken cancellationToken);
}
