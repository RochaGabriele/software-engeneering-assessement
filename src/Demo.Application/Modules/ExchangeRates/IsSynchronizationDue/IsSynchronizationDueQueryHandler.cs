using Demo.Application.Common;
using Demo.Domain.Modules.ExchangeRates;

namespace Demo.Application.Modules.ExchangeRates.IsSynchronizationDue;

/// <summary>
/// A synchronization is due when a supported currency has no stored rate (fresh database, newly configured currency, or one
/// the provider left out) or was last retrieved before the most recent scheduled run, i.e. that run did not happen.
/// Comparing with the schedule rather than with an age keeps off-schedule runs (manual triggers, late retries) from
/// hiding a missed one.
/// </summary>
public sealed class IsSynchronizationDueQueryHandler : IQueryHandler<IsSynchronizationDueQuery, bool>
{
    private readonly IExchangeRateRepository _exchangeRates;
    private readonly SupportedCurrencies _supportedCurrencies;

    public IsSynchronizationDueQueryHandler(IExchangeRateRepository exchangeRates, SupportedCurrencies supportedCurrencies)
    {
        _exchangeRates = exchangeRates;
        _supportedCurrencies = supportedCurrencies;
    }

    public async Task<bool> HandleAsync(IsSynchronizationDueQuery query, CancellationToken cancellationToken)
    {
        var retrievedAtByCurrency = (await _exchangeRates.ListAsync(cancellationToken))
            .Where(rate => rate.BaseCurrency == _supportedCurrencies.Base)
            .ToDictionary(rate => rate.QuoteCurrency, rate => rate.RetrievedAt);

        return _supportedCurrencies.Targets.Any(target =>
            !retrievedAtByCurrency.TryGetValue(target, out var retrievedAt)
            || retrievedAt < query.LastScheduledRunAt);
    }
}
