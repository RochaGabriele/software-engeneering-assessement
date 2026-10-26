using Demo.Application.Common;
using Demo.Application.Modules.ExchangeRates.Contracts;
using Demo.Domain.Common;
using Demo.Domain.Modules.Inventory;

namespace Demo.Application.Modules.Inventory.GetProductCatalog;

/// <summary>Lists the whole catalog with prices in the requested currency, using the latest stored exchange rate.</summary>
public sealed class GetProductCatalogQueryHandler : IQueryHandler<GetProductCatalogQuery, Result<IReadOnlyList<ProductDto>>>
{
    private readonly IProductRepository _products;
    private readonly ICurrencyConversionReader _conversions;

    public GetProductCatalogQueryHandler(IProductRepository products, ICurrencyConversionReader conversions)
    {
        _products = products;
        _conversions = conversions;
    }

    public async Task<Result<IReadOnlyList<ProductDto>>> HandleAsync(GetProductCatalogQuery query, CancellationToken cancellationToken)
    {
        var target = Product.BaseCurrency;

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            if (!Currency.TryParse(query.Currency, out var requested))
            {
                return InventoryErrors.InvalidCurrency(query.Currency, _conversions.SupportedCurrencies);
            }

            target = requested;
        }

        // Resolve the rate before touching the catalog: an invalid request costs no product I/O.
        var conversion = await _conversions.GetConversionAsync(Product.BaseCurrency, target, cancellationToken);
        if (!conversion.IsSuccess)
        {
            return conversion.Error!;
        }

        var products = await _products.ListAsync(cancellationToken);

        return Result<IReadOnlyList<ProductDto>>.Success(
            [.. products.Select(product => ProductDto.From(product, conversion.Value.Convert(product.Price)))]);
    }
}
