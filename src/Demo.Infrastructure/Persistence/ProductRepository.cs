using Demo.Domain.Modules.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Demo.Infrastructure.Persistence;

public class ProductRepository : IProductRepository
{
    private readonly DemoDbContext _dbContext;

    public ProductRepository(DemoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Product?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken) =>
        _dbContext.Products.FirstOrDefaultAsync(product => product.Sku == sku, cancellationToken);

    public async Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken) =>
        await _dbContext.Products
            .AsNoTracking()
            .OrderBy(product => product.Sku)
            .ToListAsync(cancellationToken);
}
