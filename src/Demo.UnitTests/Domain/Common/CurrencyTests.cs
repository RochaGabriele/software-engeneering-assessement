using Demo.Domain.Common;

namespace Demo.UnitTests.Domain.Common;

public class CurrencyTests
{
    [Fact]
    public void TryParse_ValidCode_NormalizesToUpperCase()
    {
        var parsed = Currency.TryParse(" eur ", out var currency);

        Assert.True(parsed);
        Assert.Equal("EUR", currency!.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("12A")]
    [InlineData("€UR")]
    public void TryParse_InvalidInput_ReturnsFalse(string? value)
    {
        var parsed = Currency.TryParse(value, out var currency);

        Assert.False(parsed);
        Assert.Null(currency);
    }

    [Fact]
    public void Create_InvalidCode_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Currency.Create("EURO"));
    }

    [Fact]
    public void Equality_SameCode_AreEqual()
    {
        var parsed = Currency.Create("usd");

        Assert.Equal(Currency.USD, parsed);
        Assert.Equal(Currency.USD.GetHashCode(), parsed.GetHashCode());
    }

    [Theory]
    [InlineData("USD", 2)]
    [InlineData("EUR", 2)]
    [InlineData("CAD", 2)]
    [InlineData("GBP", 2)]
    [InlineData("CHF", 2)]
    [InlineData("JPY", 0)]
    [InlineData("KRW", 0)]
    [InlineData("KWD", 3)]
    [InlineData("BHD", 3)]
    [InlineData("CLF", 4)]
    public void MinorUnits_FollowIso4217(string code, int expected)
    {
        Assert.Equal(expected, Currency.Create(code).MinorUnits);
    }

    [Fact]
    public void ToString_ReturnsCode()
    {
        Assert.Equal("USD", Currency.USD.ToString());
    }
}
