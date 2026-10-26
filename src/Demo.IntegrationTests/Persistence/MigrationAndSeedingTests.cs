using Microsoft.EntityFrameworkCore;

namespace Demo.IntegrationTests.Persistence;

public class MigrationAndSeedingTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await SqliteTestDatabase.CreateMigratedAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Theory]
    [InlineData("Products")]
    [InlineData("ExchangeRates")]
    [InlineData("ExchangeRateHistory")]
    public async Task Migrate_CreatesTable(string table)
    {
        var count = await _database.ScalarAsync($"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{table}'");

        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task Migrate_SeedsTheDemoCatalog()
    {
        await using var dbContext = _database.CreateContext();

        Assert.Equal(2, await dbContext.Products.CountAsync());
    }

    [Fact]
    public async Task Migrate_RunAgain_DoesNotDuplicateTheSeed()
    {
        await using (var dbContext = _database.CreateContext())
        {
            await dbContext.Database.MigrateAsync();
        }

        await using var verification = _database.CreateContext();
        Assert.Equal(2, await verification.Products.CountAsync());
    }

    [Fact]
    public async Task Model_HasNoChangesMissingFromTheMigrations()
    {
        await using var dbContext = _database.CreateContext();

        Assert.False(dbContext.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Price_IsStoredAsExactText()
    {
        // Baseline finding F-02: a decimal(18,2) column type made SQLite store prices as floating-point REAL.
        Assert.Equal("text", await _database.ScalarAsync("SELECT typeof(Price) FROM Products WHERE Sku = 'SKU1'"));
    }
}
