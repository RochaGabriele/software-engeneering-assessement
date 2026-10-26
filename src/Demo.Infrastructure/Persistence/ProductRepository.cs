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

    public async Task<Product> GetProduct(string sku)
    {
        return await _dbContext.Products.FirstAsync(p => p.Sku == sku);
    }
}
