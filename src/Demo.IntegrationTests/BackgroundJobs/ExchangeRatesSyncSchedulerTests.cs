using Demo.Application.Common;
using Demo.Application.Modules.ExchangeRates.IsSynchronizationDue;
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
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private readonly MemoryStorage _storage = new();
    private readonly SynchronizationDueStub _synchronizationDue = new();

    [Fact]
    public async Task Start_RegistersTheWeeklyRecurringJobOnMondayAt0600Utc()
    {
        await StartAsync(new ExchangeRateSyncOptions());

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
        await StartAsync(new ExchangeRateSyncOptions());
        await StartAsync(new ExchangeRateSyncOptions());

        Assert.Single(RecurringJobs());
    }

    [Fact]
    public async Task Start_SynchronizationDue_EnqueuesOneCatchUpRun()
    {
        _synchronizationDue.IsDue = true;

        await StartAsync(new ExchangeRateSyncOptions());

        Assert.Equal(1, EnqueuedCount());
    }

    [Fact]
    public async Task Start_SynchronizationNotDue_DoesNotEnqueue()
    {
        _synchronizationDue.IsDue = false;

        await StartAsync(new ExchangeRateSyncOptions());

        Assert.Equal(0, EnqueuedCount());
    }

    [Fact]
    public async Task Start_AsksWhetherDueSinceTheLastScheduledRun()
    {
        // Tuesday 09:00 UTC: the last Monday 06:00 UTC occurrence is the day before.
        await StartAsync(new ExchangeRateSyncOptions());

        Assert.Equal(new DateTimeOffset(2026, 10, 26, 6, 0, 0, TimeSpan.Zero), _synchronizationDue.ReceivedQuery?.LastScheduledRunAt);
    }

    [Fact]
    public async Task Start_CatchUpDisabled_DoesNotEnqueueEvenWhenDue()
    {
        _synchronizationDue.IsDue = true;

        await StartAsync(new ExchangeRateSyncOptions { CatchUpOnStartup = false });

        Assert.Single(RecurringJobs());
        Assert.Equal(0, EnqueuedCount());
        Assert.Null(_synchronizationDue.ReceivedQuery);
    }

    [Fact]
    public async Task Start_SyncDisabled_RegistersNothing()
    {
        _synchronizationDue.IsDue = true;

        await StartAsync(new ExchangeRateSyncOptions { Enabled = false });

        Assert.Empty(RecurringJobs());
        Assert.Equal(0, EnqueuedCount());
    }

    [Fact]
    public async Task Start_CustomScheduleAndTimeZone_AreHonored()
    {
        await StartAsync(new ExchangeRateSyncOptions { Cron = "30 7 * * 1", TimeZone = "Europe/Lisbon" });

        var job = Assert.Single(RecurringJobs());
        Assert.Equal("30 7 * * 1", job.Cron);
        Assert.Equal("Europe/Lisbon", job.TimeZoneId);
    }

    [Fact]
    public async Task Start_InvalidCron_FailsStartup()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => StartAsync(new ExchangeRateSyncOptions { Cron = "not a cron" }));
    }

    private async Task StartAsync(ExchangeRateSyncOptions options)
    {
        var scopeFactory = new ServiceCollection()
            .AddSingleton<IQueryHandler<IsSynchronizationDueQuery, bool>>(_synchronizationDue)
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        var scheduler = new ExchangeRatesSyncScheduler(
            new RecurringJobManager(_storage),
            new BackgroundJobClient(_storage),
            scopeFactory,
            Options.Create(options),
            new FixedTimeProvider(Now),
            NullLogger<ExchangeRatesSyncScheduler>.Instance);

        await scheduler.StartAsync(CancellationToken.None);
    }

    private List<RecurringJobDto> RecurringJobs()
    {
        using var connection = _storage.GetConnection();
        return connection.GetRecurringJobs();
    }

    private long EnqueuedCount() => _storage.GetMonitoringApi().EnqueuedCount("default");

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class SynchronizationDueStub : IQueryHandler<IsSynchronizationDueQuery, bool>
    {
        public bool IsDue { get; set; }

        public IsSynchronizationDueQuery? ReceivedQuery { get; private set; }

        public Task<bool> HandleAsync(IsSynchronizationDueQuery query, CancellationToken cancellationToken)
        {
            ReceivedQuery = query;
            return Task.FromResult(IsDue);
        }
    }
}
