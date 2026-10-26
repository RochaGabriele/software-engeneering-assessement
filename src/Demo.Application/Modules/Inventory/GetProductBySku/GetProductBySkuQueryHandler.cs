using Demo.Application.Common;
using Demo.Domain.Modules.Inventory;

namespace Demo.Application.Modules.Inventory.GetProductBySku;

public sealed class GetProductBySkuQueryHandler : IQueryHandler<GetProductBySkuQuery, Result<ProductDto>>
{
    private readonly IProductRepository _products;

    public GetProductBySkuQueryHandler(IProductRepository products)
    {
        _products = products;
    }

    public async Task<Result<ProductDto>> HandleAsync(GetProductBySkuQuery query, CancellationToken cancellationToken)
    {
        // A value that cannot be a SKU cannot identify a product either: same answer as an unknown SKU.
        if (!Sku.TryCreate(query.Sku, out var sku))
        {
            return InventoryErrors.ProductNotFound(query.Sku);
        }

        var product = await _products.GetBySkuAsync(sku, cancellationToken);

        return product is null
            ? InventoryErrors.ProductNotFound(query.Sku)
            : Result<ProductDto>.Success(ProductDto.From(product, product.Price.RoundToMinorUnits()));
    }
}
