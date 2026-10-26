using Demo.Application.Modules.ExchangeRates;
using Microsoft.Extensions.Options;

namespace Demo.UnitTests.Application.ExchangeRates;

public class ExchangeRatesOptionsValidatorTests
{
    [Fact]
    public void Validate_ValidTargets_Succeeds()
    {
        Assert.True(Validate("EUR", "CAD", "GBP", "CHF").Succeeded);
    }

    [Fact]
    public void Validate_NoTargets_Fails()
    {
        var result = Validate();

        Assert.True(result.Failed);
        Assert.Contains("TargetCurrencies", result.FailureMessage);
    }

    [Fact]
    public void Validate_InvalidCode_FailsNamingTheValue()
    {
        var result = Validate("EUR", "EURO");

        Assert.True(result.Failed);
        Assert.Contains("EURO", result.FailureMessage);
    }

    [Fact]
    public void Validate_ContainsBaseCurrency_Fails()
    {
        var result = Validate("USD", "EUR");

        Assert.True(result.Failed);
        Assert.Contains("USD", result.FailureMessage);
    }

    [Fact]
    public void Validate_DuplicateCurrency_Fails()
    {
        var result = Validate("EUR", "eur");

        Assert.True(result.Failed);
        Assert.Contains("EUR", result.FailureMessage);
    }

    private static ValidateOptionsResult Validate(params string[] targets) =>
        new ExchangeRatesOptionsValidator().Validate(name: null, new ExchangeRatesOptions { TargetCurrencies = targets });
}
