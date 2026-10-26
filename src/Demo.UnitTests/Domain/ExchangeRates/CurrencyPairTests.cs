using Demo.Domain.Common;
using Demo.Domain.Modules.ExchangeRates;

namespace Demo.UnitTests.Domain.ExchangeRates;

public class CurrencyPairTests
{
    [Fact]
    public void Constructor_SameCurrencies_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new CurrencyPair(Currency.USD, Currency.Create("usd")));
    }

    [Fact]
    public void ToString_UsesBaseSlashQuote()
    {
        Assert.Equal("USD/EUR", new CurrencyPair(Currency.USD, Currency.Create("EUR")).ToString());
    }

    [Fact]
    public void Equality_SameCurrencies_AreEqual()
    {
        Assert.Equal(new CurrencyPair(Currency.USD, Currency.Create("EUR")), new CurrencyPair(Currency.USD, Currency.Create("eur")));
    }
}
