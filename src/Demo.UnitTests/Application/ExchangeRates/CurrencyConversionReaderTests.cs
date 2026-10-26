using Demo.Application.Common;
using Demo.Application.Modules.ExchangeRates;
using Demo.Application.Modules.ExchangeRates.Contracts;
using Demo.Domain.Common;
using Demo.Domain.Modules.ExchangeRates;
using Demo.UnitTests.Fakes;

namespace Demo.UnitTests.Application.ExchangeRates;

public class CurrencyConversionReaderTests
{
    private static readonly Currency Eur = Currency.Create("EUR");
    private static readonly Currency Cad = Currency.Create("CAD");
    private static readonly DateTimeOffset PublishedAt = new(2026, 10, 26, 6, 0, 0, TimeSpan.Zero);

    private readonly InMemoryExchangeRateRepository _exchangeRates = new(
        ExchangeRate.Create(new CurrencyPair(Currency.USD, Eur), 0.92m, PublishedAt, PublishedAt.AddMinutes(1)));

    private readonly CurrencyConversionReader _reader;

    public CurrencyConversionReaderTests()
    {
        var supported = SupportedCurrencies.From(new ExchangeRatesOptions { TargetCurrencies = ["EUR", "CAD"] });
        _reader = new CurrencyConversionReader(_exchangeRates, supported);
    }

    [Fact]
    public void SupportedCurrencies_IsBaseFollowedByTargets()
    {
        Assert.Equal([Currency.USD, Eur, Cad], _reader.SupportedCurrencies);
    }

    [Fact]
    public async Task GetConversion_SameCurrency_ReturnsIdentityWithoutReadingStorage()
    {
        var result = await _reader.GetConversionAsync(Currency.USD, Currency.USD, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CurrencyConversion.Identity(Currency.USD), result.Value);
        Assert.Equal(0, _exchangeRates.GetCallCount);
    }

    [Fact]
    public async Task GetConversion_StoredRate_ReturnsItsConversion()
    {
        var result = await _reader.GetConversionAsync(Currency.USD, Eur, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new CurrencyConversion(Currency.USD, Eur, 0.92m, PublishedAt), result.Value);
    }

    [Fact]
    public async Task GetConversion_SupportedButNotStoredYet_ReturnsUnavailable()
    {
        var result = await _reader.GetConversionAsync(Currency.USD, Cad, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unavailable, result.Error!.Type);
        Assert.Equal("exchange_rate.unavailable", result.Error.Code);
        Assert.Contains("USD/CAD", result.Error.Message);
    }

    [Fact]
    public async Task GetConversion_UnsupportedTarget_ReturnsValidationListingSupportedCurrencies()
    {
        var result = await _reader.GetConversionAsync(Currency.USD, Currency.Create("JPY"), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("currency.unsupported", result.Error.Code);
        Assert.Contains("JPY", result.Error.Message);
        Assert.Contains("USD, EUR, CAD", result.Error.Message);
    }

    [Fact]
    public async Task GetConversion_UnsupportedSource_ReturnsValidation()
    {
        var result = await _reader.GetConversionAsync(Currency.Create("JPY"), Currency.USD, CancellationToken.None);

        Assert.Equal("currency.unsupported", result.Error!.Code);
    }

    [Fact]
    public async Task GetConversion_FromNonBaseCurrency_ReturnsUnsupportedConversion()
    {
        var result = await _reader.GetConversionAsync(Eur, Cad, CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("conversion.unsupported", result.Error.Code);
        Assert.Equal(0, _exchangeRates.GetCallCount);
    }
}
