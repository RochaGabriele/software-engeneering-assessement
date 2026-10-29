using Demo.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.IntegrationTests.Persistence;

public class DatabaseMigrationTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _services = new ServiceCollection()
            .AddDbContext<DemoDbContext>(options => options.UseSqlite(_connection).UseDemoSeeding())
            .BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task MigrateDatabase_EmptyDatabase_CreatesTheSchemaAndSeeds()
    {
        await _services.MigrateDatabaseAsync();

        await using var scope = _services.CreateAsyncScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<DemoDbContext>().Products.CountAsync());
    }

    [Fact]
    public async Task MigrateDatabase_OnlyEfBookkeepingTablesLeftByAnInterruptedFirstRun_Migrates()
    {
        // A first MigrateAsync killed after EF created its own tables but before InitialCreate was recorded.
        await ExecuteAsync("""
            CREATE TABLE "__EFMigrationsHistory" ("MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY, "ProductVersion" TEXT NOT NULL);
            CREATE TABLE "__EFMigrationsLock" ("Id" INTEGER NOT NULL CONSTRAINT "PK___EFMigrationsLock" PRIMARY KEY, "Timestamp" TEXT NOT NULL);
            """);

        await _services.MigrateDatabaseAsync();

        await using var scope = _services.CreateAsyncScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<DemoDbContext>().Products.CountAsync());
    }

    [Fact]
    public async Task MigrateDatabase_DatabaseCreatedByTheOriginalEnsureCreated_FailsWithActionableMessage()
    {
        // Exact schema the original starter created with EnsureCreated: tables, but no __EFMigrationsHistory.
        await ExecuteAsync("""
            CREATE TABLE "Products" ("Sku" TEXT NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY, "Name" TEXT NOT NULL, "Price" decimal(18,2) NOT NULL);
            INSERT INTO "Products" VALUES ('SKU1', 'Name', 103.3);
            """);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _services.MigrateDatabaseAsync());

        Assert.Contains("without EF Core migrations", exception.Message);
        Assert.Contains("Delete", exception.Message);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
