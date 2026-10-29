using Demo.Application.Modules.ExchangeRates;
using Demo.Domain.Modules.Inventory;

namespace Demo.UnitTests.Application;

/// <summary>Facts owned by different bounded contexts that must nevertheless agree for the system to work.</summary>
public class CrossContextInvariantsTests
{
    [Fact]
    public void CatalogPriceCurrency_IsTheExchangeRateBase()
    {
        // The catalog converts from Product.BaseCurrency, and the conversion contract only converts from the exchange-rate
        // base: if one changed alone, every non-USD catalog request would answer 400 conversion.unsupported.
        Assert.Equal(Product.BaseCurrency, SupportedCurrencies.BaseCurrency);
    }
}
