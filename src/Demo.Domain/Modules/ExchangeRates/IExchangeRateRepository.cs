namespace Demo.Domain.Modules.ExchangeRates;

public interface IExchangeRateRepository
{
    Task<ExchangeRate?> GetAsync(CurrencyPair pair, CancellationToken cancellationToken);

    /// <summary>The current rate of every stored pair.</summary>
    Task<IReadOnlyList<ExchangeRate>> ListAsync(CancellationToken cancellationToken);

    void Add(ExchangeRate exchangeRate);
}
