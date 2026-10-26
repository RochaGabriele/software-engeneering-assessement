using Demo.Application.Common;
using Demo.Domain.Common;

namespace Demo.Application.Modules.Inventory;

public static class InventoryErrors
{
    public static Error ProductNotFound(string sku) =>
        Error.NotFound("product.not_found", $"No product with SKU '{sku}' exists.");

    public static Error InvalidCurrency(string value, IEnumerable<Currency> supported) =>
        Error.Validation(
            "currency.invalid",
            $"'{value}' is not a valid ISO 4217 currency code. Supported currencies: {string.Join(", ", supported.Select(c => c.Code))}.");
}
