using Demo.Domain.Common;
using Demo.Domain.Modules.ExchangeRates;

namespace Demo.UnitTests.Domain.ExchangeRates;

public class ExchangeRateHistoryEntryTests
{
    [Fact]
    public void Record_CopiesTheCurrentStateOfTheRate()
    {
        var publishedAt = new DateTimeOffset(2026, 10, 26, 6, 0, 0, TimeSpan.Zero);
        var rate = ExchangeRate.Create(new CurrencyPair(Currency.USD, Currency.Create("CHF")), 0.7987m, publishedAt, publishedAt.AddMinutes(2));

        var entry = ExchangeRateHistoryEntry.Record(rate);

        Assert.Equal(0, entry.Id);
        Assert.Equal(Currency.USD, entry.BaseCurrency);
        Assert.Equal(Currency.Create("CHF"), entry.QuoteCurrency);
        Assert.Equal(0.7987m, entry.Rate);
        Assert.Equal(publishedAt, entry.PublishedAt);
        Assert.Equal(publishedAt.AddMinutes(2), entry.RetrievedAt);
    }
}
