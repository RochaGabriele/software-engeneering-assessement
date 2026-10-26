using Demo.Application.Common;
using Demo.Domain.Common;
using Demo.Domain.Modules.ExchangeRates;
using Demo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Demo.IntegrationTests.Persistence;

public class ExchangeRatePersistenceTests : IAsyncLifetime
{
    private static readonly CurrencyPair UsdEur = new(Currency.USD, Currency.Create("EUR"));
    private static readonly DateTimeOffset T0 = new(2026, 10, 26, 6, 0, 0, TimeSpan.Zero);

    private SqliteTestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await SqliteTestDatabase.CreateMigratedAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Rate_RoundTripsWithFullPrecisionAndTimestamps()
    {
        await SaveAsync(ExchangeRate.Create(UsdEur, 0.92123456m, T0, T0.AddSeconds(30)));

        await using var dbContext = _database.CreateContext();
        var stored = await new ExchangeRateRepository(dbContext).GetAsync(UsdEur, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(0.92123456m, stored.Rate);
        Assert.Equal(T0, stored.PublishedAt);
        Assert.Equal(T0.AddSeconds(30), stored.RetrievedAt);
    }

    [Fact]
    public async Task Rate_IsStoredAsTextAndTimestampsAsUnixMilliseconds()
    {
        await SaveAsync(ExchangeRate.Create(UsdEur, 0.92123456m, T0, T0));

        Assert.Equal("text", await _database.ScalarAsync("SELECT typeof(Rate) FROM ExchangeRates"));
        Assert.Equal("integer", await _database.ScalarAsync("SELECT typeof(PublishedAt) FROM ExchangeRates"));
        Assert.Equal(T0.ToUnixTimeMilliseconds(), await _database.ScalarAsync("SELECT PublishedAt FROM ExchangeRates"));
    }

    [Fact]
    public async Task GetAsync_UnknownPair_ReturnsNull()
    {
        await using var dbContext = _database.CreateContext();

        Assert.Null(await new ExchangeRateRepository(dbContext).GetAsync(UsdEur, CancellationToken.None));
    }

    [Fact]
    public async Task AnyAsync_ReflectsWhetherRatesAreStored()
    {
        await using (var dbContext = _database.CreateContext())
        {
            Assert.False(await new ExchangeRateRepository(dbContext).AnyAsync(CancellationToken.None));
        }

        await SaveAsync(ExchangeRate.Create(UsdEur, 0.92m, T0, T0));

        await using var verification = _database.CreateContext();
        Assert.True(await new ExchangeRateRepository(verification).AnyAsync(CancellationToken.None));
    }

    [Fact]
    public async Task UpdatedRate_IsPersisted()
    {
        await SaveAsync(ExchangeRate.Create(UsdEur, 0.92m, T0, T0));

        await using (var dbContext = _database.CreateContext())
        {
            var stored = await new ExchangeRateRepository(dbContext).GetAsync(UsdEur, CancellationToken.None);
            Assert.True(stored!.Update(0.93m, T0.AddDays(7), T0.AddDays(7)));
            await dbContext.SaveChangesAsync();
        }

        await using var verification = _database.CreateContext();
        var updated = await new ExchangeRateRepository(verification).GetAsync(UsdEur, CancellationToken.None);
        Assert.Equal(0.93m, updated!.Rate);
        Assert.Equal(T0.AddDays(7), updated.PublishedAt);
    }

    [Fact]
    public async Task UnitOfWork_CommitsRateAndHistoryTogether()
    {
        await using (var dbContext = _database.CreateContext())
        {
            var rate = ExchangeRate.Create(UsdEur, 0.92m, T0, T0);
            new ExchangeRateRepository(dbContext).Add(rate);
            new ExchangeRateHistoryRepository(dbContext).Add(ExchangeRateHistoryEntry.Record(rate));

            IUnitOfWork unitOfWork = dbContext;
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        await using var verification = _database.CreateContext();
        Assert.Equal(1, await verification.ExchangeRates.CountAsync());
        Assert.Equal(1, await verification.ExchangeRateHistory.CountAsync());
    }

    [Fact]
    public async Task History_NewPublication_IsAppended()
    {
        await AppendHistoryAsync(ExchangeRate.Create(UsdEur, 0.92m, T0, T0));
        await AppendHistoryAsync(ExchangeRate.Create(UsdEur, 0.93m, T0.AddDays(7), T0.AddDays(7)));

        await using var verification = _database.CreateContext();
        Assert.Equal(2, await verification.ExchangeRateHistory.CountAsync());
    }

    [Fact]
    public async Task History_SamePairAndPublicationTwice_ViolatesTheUniqueIndex()
    {
        await AppendHistoryAsync(ExchangeRate.Create(UsdEur, 0.92m, T0, T0));

        await Assert.ThrowsAsync<DbUpdateException>(() => AppendHistoryAsync(ExchangeRate.Create(UsdEur, 0.92m, T0, T0.AddHours(1))));
    }

    private async Task SaveAsync(ExchangeRate rate)
    {
        await using var dbContext = _database.CreateContext();
        new ExchangeRateRepository(dbContext).Add(rate);
        await dbContext.SaveChangesAsync();
    }

    private async Task AppendHistoryAsync(ExchangeRate rate)
    {
        await using var dbContext = _database.CreateContext();
        new ExchangeRateHistoryRepository(dbContext).Add(ExchangeRateHistoryEntry.Record(rate));
        await dbContext.SaveChangesAsync();
    }
}
