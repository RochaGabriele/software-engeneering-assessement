using System.Globalization;
using Demo.Domain.Common;

namespace Demo.UnitTests.Domain.Common;

public class MoneyTests
{
    [Fact]
    public void RoundToMinorUnits_NormalizesScaleToTwoDecimals()
    {
        var rounded = new Money(103.3m, Currency.USD).RoundToMinorUnits();

        Assert.Equal("103.30", rounded.Amount.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(Currency.USD, rounded.Currency);
    }

    // Midpoints where banker's rounding (the Math.Round default) would round down to the even digit.
    [Theory]
    [InlineData("0.125", "0.13")]
    [InlineData("0.145", "0.15")]
    [InlineData("-0.125", "-0.13")]
    [InlineData("95.036", "95.04")]
    public void RoundToMinorUnits_RoundsHalfAwayFromZero(string amount, string expected)
    {
        var money = new Money(decimal.Parse(amount, CultureInfo.InvariantCulture), Currency.USD);

        var rounded = money.RoundToMinorUnits();

        Assert.Equal(expected, rounded.Amount.ToString(CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("JPY", "15734.567", "15735")]
    [InlineData("JPY", "15734.4", "15734")]
    [InlineData("KWD", "1.23456", "1.235")]
    [InlineData("KWD", "1.2", "1.200")]
    public void RoundToMinorUnits_UsesTheCurrencyMinorUnits(string currency, string amount, string expected)
    {
        var money = new Money(decimal.Parse(amount, CultureInfo.InvariantCulture), Currency.Create(currency));

        Assert.Equal(expected, money.RoundToMinorUnits().Amount.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Constructor_NullCurrency_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Money(1m, null!));
    }

    [Fact]
    public void Equality_SameValueAndCurrency_AreEqual()
    {
        Assert.Equal(new Money(10.5m, Currency.USD), new Money(10.50m, Currency.USD));
    }
}
