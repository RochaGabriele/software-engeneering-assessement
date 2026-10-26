using Microsoft.EntityFrameworkCore;

namespace Demo.Infrastructure.Persistence;

internal static class DemoDbContextOptions
{
    /// <summary>Seeds the demo catalog whenever the schema is created or migrated (EF Core UseSeeding/UseAsyncSeeding).</summary>
    public static DbContextOptionsBuilder UseDemoSeeding(this DbContextOptionsBuilder options) =>
        options
            .UseSeeding((dbContext, _) => DemoDataSeeder.Seed((DemoDbContext)dbContext))
            .UseAsyncSeeding((dbContext, _, cancellationToken) => DemoDataSeeder.SeedAsync((DemoDbContext)dbContext, cancellationToken));
}
