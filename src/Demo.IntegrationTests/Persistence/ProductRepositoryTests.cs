using Demo.Domain.Common;
using Demo.Domain.Modules.Inventory;
using Demo.Infrastructure.Persistence;

namespace Demo.IntegrationTests.Persistence;

public class ProductRepositoryTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await SqliteTestDatabase.CreateMigratedAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task ListAsync_ReturnsEveryProductOrderedBySku()
    {
        // Inserted after the seed (SKU1, SKU2): without ORDER BY, SQLite would return it last.
        await using (var dbContext = _database.CreateContext())
        {
            dbContext.Products.Add(Product.Create(Sku.Create("SKU0"), "First", new Money(1m, Currency.USD)));
            await dbContext.SaveChangesAsync();
        }

        await using var verification = _database.CreateContext();
        var products = await new ProductRepository(verification).ListAsync(CancellationToken.None);

        Assert.Equal(["SKU0", "SKU1", "SKU2"], products.Select(product => product.Sku.Value));
    }

    [Fact]
    public async Task GetBySkuAsync_KnownSku_ReturnsProductWithItsPrice()
    {
        await using var dbContext = _database.CreateContext();

        var product = await new ProductRepository(dbContext).GetBySkuAsync(Sku.Create("SKU1"), CancellationToken.None);

        Assert.NotNull(product);
        Assert.Equal("Name", product.Name);
        Assert.Equal(new Money(103.30m, Currency.USD), product.Price);
    }

    [Fact]
    public async Task GetBySkuAsync_UnknownSku_ReturnsNull()
    {
        await using var dbContext = _database.CreateContext();

        Assert.Null(await new ProductRepository(dbContext).GetBySkuAsync(Sku.Create("NOPE"), CancellationToken.None));
    }
}
