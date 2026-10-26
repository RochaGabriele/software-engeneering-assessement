using Demo.Domain.Modules.Inventory;

namespace Demo.UnitTests.Fakes;

/// <summary>Returns products in insertion order; ordering by SKU is the real repository's job (covered by integration tests).</summary>
public sealed class InMemoryProductRepository : IProductRepository
{
    private readonly List<Product> _products;

    public InMemoryProductRepository(params Product[] products) => _products = [.. products];

    public int ListCallCount { get; private set; }

    public Task<Product?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken) =>
        Task.FromResult(_products.SingleOrDefault(product => product.Sku == sku));

    public Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken)
    {
        ListCallCount++;
        return Task.FromResult<IReadOnlyList<Product>>(_products);
    }
}
