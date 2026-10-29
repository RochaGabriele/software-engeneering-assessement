using Demo.Application.Modules.ExchangeRates;
using Demo.Application.Modules.ExchangeRates.IsSynchronizationDue;
using Demo.Domain.Common;
using Demo.Domain.Modules.ExchangeRates;
using Demo.UnitTests.Fakes;

namespace Demo.UnitTests.Application.ExchangeRates;

public class IsSynchronizationDueQueryHandlerTests
{
    // Tuesday after the scheduled Monday 06:00 UTC run.
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(7);

    private readonly InMemoryExchangeRateRepository _exchangeRates = new();

    [Fact]
    public async Task NothingStored_IsDue()
    {
        Assert.True(await IsDueAsync());
    }

    [Fact]
    public async Task EverySupportedRateRetrievedRecently_IsNotDue()
    {
        Store(Now.AddDays(-1), "EUR", "CAD", "GBP", "CHF");

        Assert.False(await IsDueAsync());
    }

    [Fact]
    public async Task ASupportedCurrencyWithoutRate_IsDue()
    {
        // e.g. a currency newly added to the configuration, or one the provider left out of the last run.
        Store(Now.AddDays(-1), "EUR", "CAD", "GBP");

        Assert.True(await IsDueAsync());
    }

    [Fact]
    public async Task ARateOlderThanTheSchedulePeriod_IsDue()
    {
        // Last synced the previous Monday 06:00; the app was down at this Monday 06:00, so that run never happened.
        Store(Now.AddDays(-1), "EUR", "CAD", "GBP");
        Store(Now.AddDays(-8).AddHours(-3), "CHF");

        Assert.True(await IsDueAsync());
    }

    [Fact]
    public async Task RatesForCurrenciesNoLongerConfigured_AreIgnored()
    {
        Store(Now.AddDays(-1), "EUR", "CAD", "GBP", "CHF");
        Store(Now.AddDays(-30), "JPY");

        Assert.False(await IsDueAsync());
    }

    private async Task<bool> IsDueAsync()
    {
        var handler = new IsSynchronizationDueQueryHandler(
            _exchangeRates,
            SupportedCurrencies.From(new ExchangeRatesOptions { TargetCurrencies = ["EUR", "CAD", "GBP", "CHF"] }),
            new FixedTimeProvider(Now));

        return await handler.HandleAsync(new IsSynchronizationDueQuery(StaleAfter), CancellationToken.None);
    }

    private void Store(DateTimeOffset retrievedAt, params string[] quoteCurrencies)
    {
        foreach (var quote in quoteCurrencies)
        {
            _exchangeRates.Add(ExchangeRate.Create(new CurrencyPair(Currency.USD, Currency.Create(quote)), 1.1m, retrievedAt, retrievedAt));
        }
    }
}
