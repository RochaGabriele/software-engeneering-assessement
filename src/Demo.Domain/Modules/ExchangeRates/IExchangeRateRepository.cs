namespace Demo.Domain.Modules.ExchangeRates;

public interface IExchangeRateRepository
{
    Task<ExchangeRate?> GetAsync(CurrencyPair pair, CancellationToken cancellationToken);

    Task<bool> AnyAsync(CancellationToken cancellationToken);

    void Add(ExchangeRate exchangeRate);
}
