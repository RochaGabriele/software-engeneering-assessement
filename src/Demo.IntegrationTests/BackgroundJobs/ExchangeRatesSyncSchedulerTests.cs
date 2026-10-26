using Demo.Domain.Modules.ExchangeRates;
using Demo.Infrastructure.BackgroundJobs;
using Hangfire;
using Hangfire.MemoryStorage;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Demo.IntegrationTests.BackgroundJobs;

public class ExchangeRatesSyncSchedulerTests
{
    private readonly MemoryStorage _storage = new();

    [Fact]
    public async Task Start_RegistersTheWeeklyRecurringJobOnMondayAt0600Utc()
    {
        await StartAsync(new ExchangeRateSyncOptions(), ratesStored: true);

        var job = Assert.Single(RecurringJobs());
        Assert.Equal(ExchangeRatesSyncJob.RecurringJobId, job.Id);
        Assert.Equal("0 6 * * 1", job.Cron);
        Assert.Equal("UTC", job.TimeZoneId);
        Assert.Equal(typeof(ExchangeRatesSyncJob), job.Job.Type);
    }

    [Fact]
    public async Task Start_CalledOnEveryStartup_KeepsASingleRecurringJob()
    {
        // MemoryStorage forgets everything on restart, so registration runs on every startup and must be idempotent.
        await StartAsync(new ExchangeRateSyncOptions(), ratesStored: true);
        await StartAsync(new ExchangeRateSyncOptions(), ratesStored: true);

        Assert.Single(RecurringJobs());
    }

    [Fact]
    public async Task Start_NoRatesStored_EnqueuesOneImmediateRun()
    {
        await StartAsync(new ExchangeRateSyncOptions(), ratesStored: false);

        Assert.Equal(1, EnqueuedCount());
    }

    [Fact]
    public async Task Start_RatesAlreadyStored_DoesNotEnqueue()
    {
        await StartAsync(new ExchangeRateSyncOptions(), ratesStored: true);

        Assert.Equal(0, EnqueuedCount());
    }

    [Fact]
    public async Task Start_BootstrapDisabled_DoesNotEnqueueEvenWhenEmpty()
    {
        await StartAsync(new ExchangeRateSyncOptions { RunOnStartupWhenEmpty = false }, ratesStored: false);

        Assert.Single(RecurringJobs());
        Assert.Equal(0, EnqueuedCount());
    }

    [Fact]
    public async Task Start_SyncDisabled_RegistersNothing()
    {
        await StartAsync(new ExchangeRateSyncOptions { Enabled = false }, ratesStored: false);

        Assert.Empty(RecurringJobs());
        Assert.Equal(0, EnqueuedCount());
    }

    [Fact]
    public async Task Start_CustomScheduleAndTimeZone_AreHonored()
    {
        await StartAsync(new ExchangeRateSyncOptions { Cron = "30 7 * * 1", TimeZone = "Europe/Lisbon" }, ratesStored: true);

        var job = Assert.Single(RecurringJobs());
        Assert.Equal("30 7 * * 1", job.Cron);
        Assert.Equal("Europe/Lisbon", job.TimeZoneId);
    }

    [Fact]
    public async Task Start_InvalidCron_FailsStartup()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => StartAsync(new ExchangeRateSyncOptions { Cron = "not a cron" }, ratesStored: true));
    }

    private async Task StartAsync(ExchangeRateSyncOptions options, bool ratesStored)
    {
        var scopeFactory = new ServiceCollection()
            .AddScoped<IExchangeRateRepository>(_ => new StoredRatesStub(ratesStored))
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        var scheduler = new ExchangeRatesSyncScheduler(
            new RecurringJobManager(_storage),
            new BackgroundJobClient(_storage),
            scopeFactory,
            Options.Create(options),
            NullLogger<ExchangeRatesSyncScheduler>.Instance);

        await scheduler.StartAsync(CancellationToken.None);
    }

    private List<RecurringJobDto> RecurringJobs()
    {
        using var connection = _storage.GetConnection();
        return connection.GetRecurringJobs();
    }

    private long EnqueuedCount() => _storage.GetMonitoringApi().EnqueuedCount("default");

    private sealed class StoredRatesStub(bool ratesStored) : IExchangeRateRepository
    {
        public Task<bool> AnyAsync(CancellationToken cancellationToken) => Task.FromResult(ratesStored);

        public Task<ExchangeRate?> GetAsync(CurrencyPair pair, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Add(ExchangeRate exchangeRate) => throw new NotSupportedException();
    }
}
