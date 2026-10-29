using Demo.Domain.Modules.ExchangeRates;

namespace Demo.UnitTests.Fakes;

public sealed class InMemoryExchangeRateRepository : IExchangeRateRepository
{
    private readonly List<ExchangeRate> _rates;

    public InMemoryExchangeRateRepository(params ExchangeRate[] rates) => _rates = [.. rates];

    public IReadOnlyList<ExchangeRate> Rates => _rates;

    public int GetCallCount { get; private set; }

    public Task<ExchangeRate?> GetAsync(CurrencyPair pair, CancellationToken cancellationToken)
    {
        GetCallCount++;
        return Task.FromResult(_rates.SingleOrDefault(rate => rate.Pair == pair));
    }

    public Task<IReadOnlyList<ExchangeRate>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExchangeRate>>([.. _rates]);

    public void Add(ExchangeRate exchangeRate) => _rates.Add(exchangeRate);
}
