using Demo.Application.Common;
using Demo.Domain.Modules.ExchangeRates;

namespace Demo.Application.Modules.ExchangeRates.IsSynchronizationDue;

/// <summary>
/// A synchronization is due when a supported currency has no stored rate (fresh database, newly configured currency,
/// or one the provider left out) or its rate was retrieved longer ago than one schedule period (a missed run).
/// </summary>
public sealed class IsSynchronizationDueQueryHandler : IQueryHandler<IsSynchronizationDueQuery, bool>
{
    private readonly IExchangeRateRepository _exchangeRates;
    private readonly SupportedCurrencies _supportedCurrencies;
    private readonly TimeProvider _timeProvider;

    public IsSynchronizationDueQueryHandler(IExchangeRateRepository exchangeRates, SupportedCurrencies supportedCurrencies, TimeProvider timeProvider)
    {
        _exchangeRates = exchangeRates;
        _supportedCurrencies = supportedCurrencies;
        _timeProvider = timeProvider;
    }

    public async Task<bool> HandleAsync(IsSynchronizationDueQuery query, CancellationToken cancellationToken)
    {
        var staleBefore = _timeProvider.GetUtcNow() - query.StaleAfter;

        var retrievedAtByCurrency = (await _exchangeRates.ListAsync(cancellationToken))
            .Where(rate => rate.BaseCurrency == _supportedCurrencies.Base)
            .ToDictionary(rate => rate.QuoteCurrency, rate => rate.RetrievedAt);

        return _supportedCurrencies.Targets.Any(target =>
            !retrievedAtByCurrency.TryGetValue(target, out var retrievedAt) || retrievedAt < staleBefore);
    }
}
