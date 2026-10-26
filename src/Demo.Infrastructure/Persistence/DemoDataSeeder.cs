using Demo.Domain.Common;
using Demo.Domain.Modules.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Demo.Infrastructure.Persistence;

/// <summary>
/// Demo catalog, inserted only into an empty database. Hooked into EF Core's UseSeeding/UseAsyncSeeding,
/// so it runs whenever the schema is created or migrated. Values are kept from the original starter.
/// </summary>
internal static class DemoDataSeeder
{
    public static void Seed(DemoDbContext dbContext)
    {
        if (dbContext.Products.Any())
        {
            return;
        }

        dbContext.Products.AddRange(CreateProducts());
        dbContext.SaveChanges();
    }

    public static async Task SeedAsync(DemoDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        dbContext.Products.AddRange(CreateProducts());
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<Product> CreateProducts() =>
    [
        Product.Create(Sku.Create("SKU1"), "Name", new Money(103.30m, Currency.USD)),
        Product.Create(Sku.Create("SKU2"), "Name", new Money(102.20m, Currency.USD))
    ];
}
