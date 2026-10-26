using System.Globalization;
using Demo.Application.Common;
using Demo.Application.Modules.ExchangeRates;
using Demo.Application.Modules.ExchangeRates.Contracts;
using Demo.Application.Modules.Inventory;
using Demo.Application.Modules.Inventory.GetProductCatalog;
using Demo.Domain.Common;
using Demo.Domain.Modules.ExchangeRates;
using Demo.Domain.Modules.Inventory;
using Demo.UnitTests.Fakes;

namespace Demo.UnitTests.Application.Inventory;

public class GetProductCatalogQueryHandlerTests
{
    private static readonly DateTimeOffset PublishedAt = new(2026, 10, 26, 6, 0, 0, TimeSpan.Zero);

    private readonly InMemoryProductRepository _products = new(
        Product.Create(Sku.Create("SKU1"), "Name", new Money(103.3m, Currency.USD)),
        Product.Create(Sku.Create("SKU2"), "Name", new Money(102.2m, Currency.USD)));

    private readonly InMemoryExchangeRateRepository _exchangeRates = new(
        ExchangeRate.Create(new CurrencyPair(Currency.USD, Currency.Create("EUR")), 0.92m, PublishedAt, PublishedAt));

    private readonly GetProductCatalogQueryHandler _handler;

    public GetProductCatalogQueryHandlerTests()
    {
        var supported = SupportedCurrencies.From(new ExchangeRatesOptions { TargetCurrencies = ["EUR", "CAD", "GBP", "CHF"] });
        _handler = new GetProductCatalogQueryHandler(_products, new CurrencyConversionReader(_exchangeRates, supported));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Handle_NoCurrency_ReturnsUsdPricesInRepositoryOrder(string? currency)
    {
        var result = await HandleAsync(currency);

        AssertCatalog(result, ("SKU1", "103.30", "USD"), ("SKU2", "102.20", "USD"));
    }

    [Fact]
    public async Task Handle_ExplicitUsd_ReturnsBasePricesWithoutReadingRates()
    {
        var result = await HandleAsync("USD");

        AssertCatalog(result, ("SKU1", "103.30", "USD"), ("SKU2", "102.20", "USD"));
        Assert.Equal(0, _exchangeRates.GetCallCount);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("eur")]
    [InlineData(" Eur ")]
    public async Task Handle_SupportedCurrency_ConvertsWithLatestStoredRate(string currency)
    {
        var result = await HandleAsync(currency);

        AssertCatalog(result, ("SKU1", "95.04", "EUR"), ("SKU2", "94.02", "EUR"));
    }

    [Fact]
    public async Task Handle_MalformedCurrency_ReturnsInvalidCurrencyListingSupportedOnes()
    {
        var result = await HandleAsync("EURO");

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("currency.invalid", result.Error.Code);
        Assert.Contains("'EURO'", result.Error.Message);
        Assert.Contains("USD, EUR, CAD, GBP, CHF", result.Error.Message);
        Assert.Equal(0, _products.ListCallCount);
    }

    [Fact]
    public async Task Handle_UnsupportedCurrency_ReturnsUnsupported()
    {
        var result = await HandleAsync("JPY");

        Assert.Equal("currency.unsupported", result.Error!.Code);
        Assert.Equal(0, _products.ListCallCount);
    }

    [Fact]
    public async Task Handle_SupportedCurrencyWithoutStoredRate_ReturnsUnavailableWithoutReadingProducts()
    {
        var result = await HandleAsync("CAD");

        Assert.Equal(ErrorType.Unavailable, result.Error!.Type);
        Assert.Equal("exchange_rate.unavailable", result.Error.Code);
        Assert.Equal(0, _products.ListCallCount);
    }

    private Task<Result<IReadOnlyList<ProductDto>>> HandleAsync(string? currency) =>
        _handler.HandleAsync(new GetProductCatalogQuery(currency), CancellationToken.None);

    private static void AssertCatalog(Result<IReadOnlyList<ProductDto>> result, params (string Sku, string Price, string Currency)[] expected)
    {
        Assert.True(result.IsSuccess);
        var actual = result.Value.Select(item => (item.Sku, item.Price.ToString(CultureInfo.InvariantCulture), item.Currency));
        Assert.Equal(expected, actual);
    }
}
