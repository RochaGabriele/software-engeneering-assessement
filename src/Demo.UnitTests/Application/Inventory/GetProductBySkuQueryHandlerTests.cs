using System.Globalization;
using Demo.Application.Common;
using Demo.Application.Modules.Inventory;
using Demo.Application.Modules.Inventory.GetProductBySku;
using Demo.Domain.Common;
using Demo.Domain.Modules.Inventory;
using Demo.UnitTests.Fakes;

namespace Demo.UnitTests.Application.Inventory;

public class GetProductBySkuQueryHandlerTests
{
    private readonly GetProductBySkuQueryHandler _handler = new(new InMemoryProductRepository(
        Product.Create(Sku.Create("SKU1"), "Name", new Money(103.3m, Currency.USD))));

    [Fact]
    public async Task Handle_KnownSku_ReturnsProductInUsdWithTwoDecimals()
    {
        var result = await _handler.HandleAsync(new GetProductBySkuQuery("SKU1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ProductDto("SKU1", "Name", 103.30m, "USD"), result.Value);
        Assert.Equal("103.30", result.Value.Price.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Handle_UnknownSku_ReturnsNotFound()
    {
        var result = await _handler.HandleAsync(new GetProductBySkuQuery("NOPE"), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("product.not_found", result.Error.Code);
        Assert.Contains("NOPE", result.Error.Message);
    }

    [Fact]
    public async Task Handle_MalformedSku_ReturnsNotFound()
    {
        var result = await _handler.HandleAsync(new GetProductBySkuQuery(new string('X', Sku.MaxLength + 1)), CancellationToken.None);

        Assert.Equal("product.not_found", result.Error!.Code);
    }
}
