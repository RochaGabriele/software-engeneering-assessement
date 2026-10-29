using Demo.Application.Common;
using Demo.Application.Modules.ExchangeRates.IsSynchronizationDue;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Demo.Infrastructure.BackgroundJobs;

/// <summary>
/// Registers the weekly sync on every startup (MemoryStorage forgets recurring jobs on restart, and AddOrUpdate is
/// idempotent), then catches up: one immediate run when a supported currency has no rate or was not refreshed since the
/// most recent scheduled occurrence. That covers a fresh deployment, a newly configured currency and a run missed while
/// the app was down, whatever happened off-schedule before (manual triggers, late retries, DST shifts) (ADR-0005).
/// </summary>
internal sealed class ExchangeRatesSyncScheduler : IHostedService
{
    private readonly IRecurringJobManager _recurringJobs;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ExchangeRateSyncOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ExchangeRatesSyncScheduler> _logger;

    public ExchangeRatesSyncScheduler(
        IRecurringJobManager recurringJobs,
        IBackgroundJobClient backgroundJobs,
        IServiceScopeFactory scopeFactory,
        IOptions<ExchangeRateSyncOptions> options,
        TimeProvider timeProvider,
        ILogger<ExchangeRatesSyncScheduler> logger)
    {
        _recurringJobs = recurringJobs;
        _backgroundJobs = backgroundJobs;
        _scopeFactory = scopeFactory;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation("Exchange rate synchronization is disabled; no job registered.");
            return;
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);

        // CancellationToken.None is a placeholder: Hangfire substitutes the job's own cancellation token at run time.
        _recurringJobs.AddOrUpdate<ExchangeRatesSyncJob>(
            ExchangeRatesSyncJob.RecurringJobId,
            job => job.ExecuteAsync(CancellationToken.None),
            options.Cron,
            new RecurringJobOptions { TimeZone = timeZone });

        _logger.LogInformation(
            "Recurring job {JobId} scheduled with cron '{Cron}' ({TimeZone}).",
            ExchangeRatesSyncJob.RecurringJobId,
            options.Cron,
            options.TimeZone);

        if (!options.CatchUpOnStartup)
        {
            return;
        }

        var lastScheduledRunAt = CronSchedule.PreviousOccurrence(options.Cron, timeZone, _timeProvider.GetUtcNow());

        await using var scope = _scopeFactory.CreateAsyncScope();
        var isSynchronizationDue = scope.ServiceProvider.GetRequiredService<IQueryHandler<IsSynchronizationDueQuery, bool>>();

        if (await isSynchronizationDue.HandleAsync(new IsSynchronizationDueQuery(lastScheduledRunAt), cancellationToken))
        {
            _backgroundJobs.Enqueue<ExchangeRatesSyncJob>(job => job.ExecuteAsync(CancellationToken.None));
            _logger.LogInformation(
                "A supported currency has no rate or was not refreshed since the scheduled run of {LastScheduledRunAt:O}; enqueued a catch-up synchronization.",
                lastScheduledRunAt);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
