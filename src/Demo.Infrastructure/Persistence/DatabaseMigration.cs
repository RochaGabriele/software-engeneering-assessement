using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.Infrastructure.Persistence;

public static class DatabaseMigration
{
    /// <summary>
    /// Applies pending EF Core migrations (which also runs the demo seeding). Done at startup to keep the
    /// "database is created on first run" experience; production would run migrations as a deployment step (ADR-0004).
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DemoDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
