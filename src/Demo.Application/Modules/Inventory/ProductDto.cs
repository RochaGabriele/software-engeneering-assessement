using Demo.Domain.Common;
using Demo.Domain.Modules.Inventory;

namespace Demo.Application.Modules.Inventory;

/// <summary>Public representation of a product, with its price in the currency stated alongside it.</summary>
public sealed record ProductDto(string Sku, string Name, decimal Price, string Currency)
{
    public static ProductDto From(Product product, Money price) =>
        new(product.Sku.Value, product.Name, price.Amount, price.Currency.Code);
}
