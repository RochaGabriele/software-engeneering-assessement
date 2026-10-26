using System.Globalization;
using Demo.Domain.Common;

namespace Demo.UnitTests.Domain.Common;

public class CurrencyConversionTests
{
    private static readonly Currency Eur = Currency.Create("EUR");
    private static readonly DateTimeOffset PublishedAt = new(2026, 10, 26, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Convert_MultipliesAmountByRate()
    {
        // Open Exchange Rates quotes "1 USD = 0.92 EUR": a USD amount is multiplied, never divided.
        var conversion = new CurrencyConversion(Currency.USD, Eur, 0.92m, PublishedAt);

        var converted = conversion.Convert(new Money(100m, Currency.USD));

        Assert.Equal(Eur, converted.Currency);
        Assert.Equal("92.00", Format(converted));
    }

    [Fact]
    public void Convert_UsesFullPrecisionRateAndRoundsOnceAtTheEnd()
    {
        // 103.30 × 0.921234 = 95.1634722 → 95.16. Rounding the rate first (0.92) would give 95.04.
        var conversion = new CurrencyConversion(Currency.USD, Eur, 0.921234m, PublishedAt);

        var converted = conversion.Convert(new Money(103.30m, Currency.USD));

        Assert.Equal("95.16", Format(converted));
    }

    [Theory]
    [InlineData("103.30", "95.04")]
    [InlineData("102.20", "94.02")]
    public void Convert_SpecExampleWithRate092(string usdAmount, string expectedEur)
    {
        var conversion = new CurrencyConversion(Currency.USD, Eur, 0.92m, PublishedAt);

        var converted = conversion.Convert(new Money(decimal.Parse(usdAmount, CultureInfo.InvariantCulture), Currency.USD));

        Assert.Equal(expectedEur, Format(converted));
    }

    [Fact]
    public void Convert_MoneyInAnotherCurrency_ThrowsDomainException()
    {
        var conversion = new CurrencyConversion(Currency.USD, Eur, 0.92m, PublishedAt);

        Assert.Throws<DomainException>(() => conversion.Convert(new Money(10m, Eur)));
    }

    [Fact]
    public void Identity_KeepsAmountAndNormalizesScale()
    {
        var identity = CurrencyConversion.Identity(Currency.USD);

        var converted = identity.Convert(new Money(102.2m, Currency.USD));

        Assert.Equal(1m, identity.Rate);
        Assert.Null(identity.AsOf);
        Assert.Equal(Currency.USD, converted.Currency);
        Assert.Equal("102.20", Format(converted));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void Constructor_NonPositiveRate_ThrowsDomainException(string rate)
    {
        Assert.Throws<DomainException>(() =>
            new CurrencyConversion(Currency.USD, Eur, decimal.Parse(rate, CultureInfo.InvariantCulture), PublishedAt));
    }

    [Fact]
    public void Constructor_ExposesConversionDetails()
    {
        var conversion = new CurrencyConversion(Currency.USD, Eur, 0.92m, PublishedAt);

        Assert.Equal(Currency.USD, conversion.From);
        Assert.Equal(Eur, conversion.To);
        Assert.Equal(0.92m, conversion.Rate);
        Assert.Equal(PublishedAt, conversion.AsOf);
    }

    private static string Format(Money money) => money.Amount.ToString(CultureInfo.InvariantCulture);
}
