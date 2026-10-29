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

        await EnsureNotCreatedWithoutMigrationsAsync(dbContext, cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    // The original starter created the schema with EnsureCreated, which records no migration history. Migrating such a
    // database fails with a cryptic "table already exists"; explain what to do instead of deleting data on our own.
    // Only application tables count: EF's own bookkeeping tables left by an interrupted first migration are harmless.
    private static async Task EnsureNotCreatedWithoutMigrationsAsync(DemoDbContext dbContext, CancellationToken cancellationToken)
    {
        if ((await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).Any())
        {
            return;
        }

        var applicationTables = dbContext.Model.GetEntityTypes()
            .Select(entityType => entityType.GetTableName())
            .OfType<string>()
            .ToHashSet();

        var existingTables = await dbContext.Database
            .SqlQueryRaw<string>("""SELECT name AS "Value" FROM sqlite_master WHERE type = 'table'""")
            .ToListAsync(cancellationToken);

        if (existingTables.Any(applicationTables.Contains))
        {
            throw new InvalidOperationException(
                $"The database '{dbContext.Database.GetDbConnection().DataSource}' was created without EF Core migrations " +
                "(for example by the original starter's EnsureCreated), so its schema cannot be upgraded. " +
                "Delete it (src/Demo.Api/demo.db, demo.db-shm and demo.db-wal) and restart: it is recreated and re-seeded automatically.");
        }
    }
}
