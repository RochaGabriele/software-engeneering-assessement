using Demo.Domain.Common;
using Demo.Domain.Modules.ExchangeRates;
using Demo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.IntegrationTests.Api;

/// <summary>
/// The real API, wired exactly as in production, over a throwaway SQLite file. The "Testing" environment never loads
/// user-secrets, so a developer's real App ID cannot leak into tests; the scheduled sync is disabled so no background
/// job runs unless a test triggers it.
/// </summary>
public sealed class DemoApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset RatesPublishedAt = new(2026, 10, 26, 6, 0, 0, TimeSpan.Zero);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"demo-api-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Demo", $"Data Source={_databasePath}");
        builder.UseSetting("ExchangeRates:Sync:Enabled", "false");
    }

    public async Task StoreRateAsync(string quoteCurrency, decimal rate)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DemoDbContext>();

        dbContext.ExchangeRates.Add(ExchangeRate.Create(
            new CurrencyPair(Currency.USD, Currency.Create(quoteCurrency)), rate, RatesPublishedAt, RatesPublishedAt));
        await dbContext.SaveChangesAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        // Pooled connections keep the file open on Windows; release them before deleting.
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            File.Delete(_databasePath + suffix);
        }
    }
}
