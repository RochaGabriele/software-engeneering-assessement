using Demo.Application.Modules.ExchangeRates.Ports;
using Demo.Domain.Common;

namespace Demo.UnitTests.Fakes;

public sealed class StubExchangeRateProvider : IExchangeRateProvider
{
    private readonly LatestRates? _latestRates;
    private readonly Exception? _exception;

    private StubExchangeRateProvider(LatestRates? latestRates, Exception? exception)
    {
        _latestRates = latestRates;
        _exception = exception;
    }

    public Currency? RequestedBase { get; private set; }

    public IReadOnlyCollection<Currency>? RequestedTargets { get; private set; }

    public static StubExchangeRateProvider Returning(LatestRates latestRates) => new(latestRates, null);

    public static StubExchangeRateProvider Throwing(Exception exception) => new(null, exception);

    public Task<LatestRates> GetLatestRatesAsync(Currency baseCurrency, IReadOnlyCollection<Currency> targets, CancellationToken cancellationToken)
    {
        RequestedBase = baseCurrency;
        RequestedTargets = targets;

        return _exception is null ? Task.FromResult(_latestRates!) : Task.FromException<LatestRates>(_exception);
    }
}
