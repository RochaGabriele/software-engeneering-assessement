using Demo.Application.Common;
using Demo.Application.Modules.ExchangeRates;
using Demo.Application.Modules.ExchangeRates.Ports;
using Demo.Application.Modules.ExchangeRates.SynchronizeExchangeRates;
using Demo.Domain.Common;
using Demo.Domain.Modules.ExchangeRates;
using Demo.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Demo.UnitTests.Application.ExchangeRates;

public class SynchronizeExchangeRatesCommandHandlerTests
{
    private static readonly Currency Eur = Currency.Create("EUR");
    private static readonly Currency Cad = Currency.Create("CAD");
    private static readonly Currency Gbp = Currency.Create("GBP");
    private static readonly Currency Chf = Currency.Create("CHF");
    private static readonly DateTimeOffset T0 = new(2026, 10, 26, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = T0.AddMinutes(5);

    private readonly InMemoryExchangeRateRepository _exchangeRates = new();
    private readonly InMemoryExchangeRateHistoryRepository _history = new();
    private readonly SpyUnitOfWork _unitOfWork = new();

    [Fact]
    public async Task FirstRun_CreatesRatesAndHistoryAndSavesOnce()
    {
        var result = await HandleAsync(Rates(T0, (Eur, 0.92m), (Cad, 1.40m), (Gbp, 0.75m), (Chf, 0.80m)));

        Assert.True(result.IsSuccess);
        Assert.Equal([Eur, Cad, Gbp, Chf], result.Value.Applied);
        Assert.Empty(result.Value.Unchanged);
        Assert.Empty(result.Value.Missing);
        Assert.Equal(T0, result.Value.PublishedAt);

        Assert.Equal(4, _exchangeRates.Rates.Count);
        var eur = Assert.Single(_exchangeRates.Rates, rate => rate.QuoteCurrency == Eur);
        Assert.Equal(Currency.USD, eur.BaseCurrency);
        Assert.Equal(0.92m, eur.Rate);
        Assert.Equal(T0, eur.PublishedAt);
        Assert.Equal(Now, eur.RetrievedAt);

        Assert.Equal(4, _history.Entries.Count);
        Assert.All(_history.Entries, entry => Assert.Equal(T0, entry.PublishedAt));
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task NewerPublication_UpdatesRatesAndAppendsHistory()
    {
        // Last week's rates are stored; the provider now publishes this week's (never later than the clock).
        StoreAllAt(T0.AddDays(-7));

        var result = await HandleAsync(Rates(T0, (Eur, 0.93m), (Cad, 1.41m), (Gbp, 0.76m), (Chf, 0.81m)));

        Assert.Equal(4, result.Value.Applied.Count);
        Assert.Equal(0.93m, _exchangeRates.Rates.Single(rate => rate.QuoteCurrency == Eur).Rate);
        Assert.Equal(4, _exchangeRates.Rates.Count);
        Assert.Equal(4, _history.Entries.Count);
        Assert.All(_history.Entries, entry => Assert.Equal(T0, entry.PublishedAt));
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task SamePublication_ChangesNothing()
    {
        StoreAllAt(T0);

        var result = await HandleAsync(Rates(T0, (Eur, 0.99m), (Cad, 1.40m), (Gbp, 0.75m), (Chf, 0.80m)));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Applied);
        Assert.Equal([Eur, Cad, Gbp, Chf], result.Value.Unchanged);
        Assert.Equal(0.92m, _exchangeRates.Rates.Single(rate => rate.QuoteCurrency == Eur).Rate);
        Assert.Empty(_history.Entries);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task PartialResponse_AppliesPresentCurrenciesAndReportsMissingOnes()
    {
        var result = await HandleAsync(Rates(T0, (Eur, 0.92m), (Cad, 1.40m), (Chf, 0.80m)));

        Assert.True(result.IsSuccess);
        Assert.Equal([Eur, Cad, Chf], result.Value.Applied);
        Assert.Equal([Gbp], result.Value.Missing);
        Assert.DoesNotContain(_exchangeRates.Rates, rate => rate.QuoteCurrency == Gbp);
        Assert.Equal(3, _history.Entries.Count);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task NonPositiveRate_IsTreatedAsMissing()
    {
        var result = await HandleAsync(Rates(T0, (Eur, 0.92m), (Cad, 1.40m), (Gbp, 0.75m), (Chf, 0m)));

        Assert.Equal([Chf], result.Value.Missing);
        Assert.DoesNotContain(_exchangeRates.Rates, rate => rate.QuoteCurrency == Chf);
    }

    [Fact]
    public async Task NoUsableRate_ReturnsFailureWithoutSaving()
    {
        var result = await HandleAsync(Rates(T0));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unavailable, result.Error!.Type);
        Assert.Equal("exchange_rate.no_rates_applied", result.Error.Code);
        Assert.Contains("EUR, CAD, GBP, CHF", result.Error.Message);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ProviderFailure_PropagatesWithoutSaving()
    {
        var failure = new ExchangeRateProviderException("invalid_app_id");
        var handler = CreateHandler(StubExchangeRateProvider.Throwing(failure));

        var thrown = await Assert.ThrowsAsync<ExchangeRateProviderException>(
            () => handler.HandleAsync(new SynchronizeExchangeRatesCommand(), CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Empty(_exchangeRates.Rates);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ProviderAnswersInAnotherBase_FailsWithoutStoringAnything()
    {
        // EUR-based values stored as USD→X rates would silently corrupt every converted price.
        var result = await HandleAsync(new LatestRates(Eur, T0, new Dictionary<Currency, decimal> { [Cad] = 1.52m, [Gbp] = 0.81m }));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unavailable, result.Error!.Type);
        Assert.Equal("exchange_rate.unexpected_base", result.Error.Code);
        Assert.Contains("EUR", result.Error.Message);
        Assert.Contains("USD", result.Error.Message);
        AssertNothingStored();
    }

    [Fact]
    public async Task PublicationInTheFuture_FailsWithoutStoringAnything()
    {
        // Stored as PublishedAt, a future timestamp would make every real publication look older and freeze the rates.
        var result = await HandleAsync(Rates(Now.AddDays(2), (Eur, 0.92m)));

        Assert.False(result.IsSuccess);
        Assert.Equal("exchange_rate.publication_in_future", result.Error!.Code);
        AssertNothingStored();
    }

    [Fact]
    public async Task PublicationSlightlyAheadOfTheLocalClock_IsAccepted()
    {
        // Tolerates ordinary clock skew between this server and the provider.
        var result = await HandleAsync(Rates(Now.AddMinutes(30), (Eur, 0.92m)));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RequestsUsdBaseAndConfiguredTargets()
    {
        var provider = StubExchangeRateProvider.Returning(Rates(T0, (Eur, 0.92m)));

        await CreateHandler(provider).HandleAsync(new SynchronizeExchangeRatesCommand(), CancellationToken.None);

        Assert.Equal(Currency.USD, provider.RequestedBase);
        Assert.Equal([Eur, Cad, Gbp, Chf], provider.RequestedTargets);
    }

    private Task<Result<SynchronizationReport>> HandleAsync(LatestRates latestRates) =>
        CreateHandler(StubExchangeRateProvider.Returning(latestRates))
            .HandleAsync(new SynchronizeExchangeRatesCommand(), CancellationToken.None);

    private SynchronizeExchangeRatesCommandHandler CreateHandler(IExchangeRateProvider provider) =>
        new(
            provider,
            _exchangeRates,
            _history,
            _unitOfWork,
            SupportedCurrencies.From(new ExchangeRatesOptions { TargetCurrencies = ["EUR", "CAD", "GBP", "CHF"] }),
            new FixedTimeProvider(Now),
            NullLogger<SynchronizeExchangeRatesCommandHandler>.Instance);

    private void AssertNothingStored()
    {
        Assert.Empty(_exchangeRates.Rates);
        Assert.Empty(_history.Entries);
        Assert.Equal(0, _unitOfWork.SaveChangesCallCount);
    }

    private void StoreAllAt(DateTimeOffset publishedAt)
    {
        foreach (var (quote, rate) in new[] { (Eur, 0.92m), (Cad, 1.40m), (Gbp, 0.75m), (Chf, 0.80m) })
        {
            _exchangeRates.Add(ExchangeRate.Create(new CurrencyPair(Currency.USD, quote), rate, publishedAt, publishedAt));
        }
    }

    private static LatestRates Rates(DateTimeOffset publishedAt, params (Currency Quote, decimal Rate)[] rates) =>
        new(Currency.USD, publishedAt, rates.ToDictionary(rate => rate.Quote, rate => rate.Rate));
}
