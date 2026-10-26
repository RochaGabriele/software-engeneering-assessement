namespace Demo.Application.Modules.Inventory.GetProductCatalog;

/// <param name="Currency">Optional ISO 4217 code; blank means the catalog base currency (USD).</param>
public sealed record GetProductCatalogQuery(string? Currency);
