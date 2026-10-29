using Demo.Application.Modules.ExchangeRates;
using Demo.Application.Modules.ExchangeRates.IsSynchronizationDue;
using Demo.Domain.Common;
using Demo.Domain.Modules.ExchangeRates;
using Demo.UnitTests.Fakes;

namespace Demo.UnitTests.Application.ExchangeRates;

public class IsSynchronizationDueQueryHandlerTests
{
    // The Monday 06:00 UTC run that should have happened most recently.
    private static readonly DateTimeOffset LastScheduledRun = new(2026, 10, 26, 6, 0, 0, TimeSpan.Zero);

    private readonly InMemoryExchangeRateRepository _exchangeRates = new();

    [Fact]
    public async Task NothingStored_IsDue()
    {
        Assert.True(await IsDueAsync(LastScheduledRun));
    }

    [Fact]
    public async Task EveryRateRetrievedAfterTheLastScheduledRun_IsNotDue()
    {
        Store(LastScheduledRun.AddSeconds(20), "EUR", "CAD", "GBP", "CHF");

        Assert.False(await IsDueAsync(LastScheduledRun));
    }

    [Fact]
    public async Task ARateRetrievedBeforeTheLastScheduledRun_IsDue()
    {
        // The app was down at Monday 06:00, so last week's rates were never refreshed.
        Store(LastScheduledRun.AddDays(-7).AddSeconds(20), "EUR", "CAD", "GBP", "CHF");

        Assert.True(await IsDueAsync(LastScheduledRun));
    }

    [Fact]
    public async Task AnOffScheduleRunLastWeek_DoesNotHideAMissedMonday()
    {
        // A manual trigger on Thursday is only four days old, but the Monday run after it never happened.
        Store(LastScheduledRun.AddDays(-4), "EUR", "CAD", "GBP", "CHF");

        Assert.True(await IsDueAsync(LastScheduledRun));
    }

    [Fact]
    public async Task ASupportedCurrencyWithoutRate_IsDue()
    {
        // e.g. a currency newly added to the configuration, or one the provider left out of the last run.
        Store(LastScheduledRun.AddSeconds(20), "EUR", "CAD", "GBP");

        Assert.True(await IsDueAsync(LastScheduledRun));
    }

    [Fact]
    public async Task RatesForCurrenciesNoLongerConfigured_AreIgnored()
    {
        Store(LastScheduledRun.AddSeconds(20), "EUR", "CAD", "GBP", "CHF");
        Store(LastScheduledRun.AddDays(-30), "JPY");

        Assert.False(await IsDueAsync(LastScheduledRun));
    }

    [Fact]
    public async Task NoScheduledRunYet_OnlyAMissingRateMakesItDue()
    {
        Store(LastScheduledRun.AddDays(-30), "EUR", "CAD", "GBP", "CHF");

        Assert.False(await IsDueAsync(lastScheduledRun: null));
    }

    private async Task<bool> IsDueAsync(DateTimeOffset? lastScheduledRun)
    {
        var handler = new IsSynchronizationDueQueryHandler(
            _exchangeRates,
            SupportedCurrencies.From(new ExchangeRatesOptions { TargetCurrencies = ["EUR", "CAD", "GBP", "CHF"] }));

        return await handler.HandleAsync(new IsSynchronizationDueQuery(lastScheduledRun), CancellationToken.None);
    }

    private void Store(DateTimeOffset retrievedAt, params string[] quoteCurrencies)
    {
        foreach (var quote in quoteCurrencies)
        {
            _exchangeRates.Add(ExchangeRate.Create(new CurrencyPair(Currency.USD, Currency.Create(quote)), 1.1m, retrievedAt, retrievedAt));
        }
    }
}
