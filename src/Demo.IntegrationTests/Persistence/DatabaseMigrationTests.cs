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
    public async Task MigrateDatabase_DatabaseCreatedByTheOriginalEnsureCreated_FailsWithActionableMessage()
    {
        // Exact schema the original starter created with EnsureCreated: tables, but no __EFMigrationsHistory.
        await using (var command = _connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE "Products" ("Sku" TEXT NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY, "Name" TEXT NOT NULL, "Price" decimal(18,2) NOT NULL);
                INSERT INTO "Products" VALUES ('SKU1', 'Name', 103.3);
                """;
            await command.ExecuteNonQueryAsync();
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _services.MigrateDatabaseAsync());

        Assert.Contains("without EF Core migrations", exception.Message);
        Assert.Contains("Delete", exception.Message);
    }
}
