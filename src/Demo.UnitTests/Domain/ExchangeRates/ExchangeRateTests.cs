using Demo.Domain.Common;
using Demo.Domain.Modules.ExchangeRates;

namespace Demo.UnitTests.Domain.ExchangeRates;

public class ExchangeRateTests
{
    private static readonly Currency Eur = Currency.Create("EUR");
    private static readonly CurrencyPair UsdEur = new(Currency.USD, Eur);
    private static readonly DateTimeOffset T0 = new(2026, 10, 26, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_SetsState()
    {
        var rate = ExchangeRate.Create(UsdEur, 0.92m, T0, T0.AddMinutes(1));

        Assert.Equal(Currency.USD, rate.BaseCurrency);
        Assert.Equal(Eur, rate.QuoteCurrency);
        Assert.Equal(UsdEur, rate.Pair);
        Assert.Equal(0.92m, rate.Rate);
        Assert.Equal(T0, rate.PublishedAt);
        Assert.Equal(T0.AddMinutes(1), rate.RetrievedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveRate_ThrowsDomainException(int rate)
    {
        Assert.Throws<DomainException>(() => ExchangeRate.Create(UsdEur, rate, T0, T0));
    }

    [Fact]
    public void Update_NewerPublication_AppliesAndReturnsTrue()
    {
        var rate = ExchangeRate.Create(UsdEur, 0.92m, T0, T0);

        var applied = rate.Update(0.93m, T0.AddDays(7), T0.AddDays(7).AddMinutes(1));

        Assert.True(applied);
        Assert.Equal(0.93m, rate.Rate);
        Assert.Equal(T0.AddDays(7), rate.PublishedAt);
        Assert.Equal(T0.AddDays(7).AddMinutes(1), rate.RetrievedAt);
    }

    [Fact]
    public void Update_NewerPublicationWithSameRate_StillApplies()
    {
        // A newer observation is new information even when the value did not move.
        var rate = ExchangeRate.Create(UsdEur, 0.92m, T0, T0);

        Assert.True(rate.Update(0.92m, T0.AddDays(7), T0.AddDays(7)));
        Assert.Equal(T0.AddDays(7), rate.PublishedAt);
    }

    [Fact]
    public void Update_SamePublication_IsIgnoredAndReturnsFalse()
    {
        var rate = ExchangeRate.Create(UsdEur, 0.92m, T0, T0);

        var applied = rate.Update(0.99m, T0, T0.AddHours(1));

        Assert.False(applied);
        AssertUnchanged(rate);
    }

    [Fact]
    public void Update_OlderPublication_IsIgnoredAndReturnsFalse()
    {
        var rate = ExchangeRate.Create(UsdEur, 0.92m, T0, T0);

        var applied = rate.Update(0.99m, T0.AddDays(-1), T0.AddHours(1));

        Assert.False(applied);
        AssertUnchanged(rate);
    }

    [Fact]
    public void Update_NewerPublicationWithNonPositiveRate_ThrowsDomainException()
    {
        var rate = ExchangeRate.Create(UsdEur, 0.92m, T0, T0);

        Assert.Throws<DomainException>(() => rate.Update(-1m, T0.AddDays(1), T0.AddDays(1)));
        AssertUnchanged(rate);
    }

    [Fact]
    public void ToConversion_MapsPairRateAndPublicationTime()
    {
        var conversion = ExchangeRate.Create(UsdEur, 0.92m, T0, T0.AddMinutes(1)).ToConversion();

        Assert.Equal(Currency.USD, conversion.From);
        Assert.Equal(Eur, conversion.To);
        Assert.Equal(0.92m, conversion.Rate);
        Assert.Equal(T0, conversion.AsOf);
    }

    private static void AssertUnchanged(ExchangeRate rate)
    {
        Assert.Equal(0.92m, rate.Rate);
        Assert.Equal(T0, rate.PublishedAt);
        Assert.Equal(T0, rate.RetrievedAt);
    }
}
