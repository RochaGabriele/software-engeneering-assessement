namespace Demo.Domain.Modules.Inventory;

public interface IProductRepository
{
    Task<Product?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken);

    /// <summary>Every product, ordered by SKU.</summary>
    Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken);
}
