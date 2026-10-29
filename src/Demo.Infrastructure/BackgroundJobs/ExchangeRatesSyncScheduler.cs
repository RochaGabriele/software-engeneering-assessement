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
/// idempotent), then catches up: one immediate run when a supported currency has no rate or a stale one, which covers
/// a fresh deployment, a newly configured currency and a Monday run missed while the app was down (ADR-0005).
/// </summary>
internal sealed class ExchangeRatesSyncScheduler : IHostedService
{
    private readonly IRecurringJobManager _recurringJobs;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ExchangeRateSyncOptions> _options;
    private readonly ILogger<ExchangeRatesSyncScheduler> _logger;

    public ExchangeRatesSyncScheduler(
        IRecurringJobManager recurringJobs,
        IBackgroundJobClient backgroundJobs,
        IServiceScopeFactory scopeFactory,
        IOptions<ExchangeRateSyncOptions> options,
        ILogger<ExchangeRatesSyncScheduler> logger)
    {
        _recurringJobs = recurringJobs;
        _backgroundJobs = backgroundJobs;
        _scopeFactory = scopeFactory;
        _options = options;
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

        // CancellationToken.None is a placeholder: Hangfire substitutes the job's own cancellation token at run time.
        _recurringJobs.AddOrUpdate<ExchangeRatesSyncJob>(
            ExchangeRatesSyncJob.RecurringJobId,
            job => job.ExecuteAsync(CancellationToken.None),
            options.Cron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone) });

        _logger.LogInformation(
            "Recurring job {JobId} scheduled with cron '{Cron}' ({TimeZone}).",
            ExchangeRatesSyncJob.RecurringJobId,
            options.Cron,
            options.TimeZone);

        if (!options.CatchUpOnStartup)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var isSynchronizationDue = scope.ServiceProvider.GetRequiredService<IQueryHandler<IsSynchronizationDueQuery, bool>>();

        if (await isSynchronizationDue.HandleAsync(new IsSynchronizationDueQuery(options.StaleAfter), cancellationToken))
        {
            _backgroundJobs.Enqueue<ExchangeRatesSyncJob>(job => job.ExecuteAsync(CancellationToken.None));
            _logger.LogInformation(
                "Exchange rates are missing or older than {StaleAfter}; enqueued a catch-up synchronization.",
                options.StaleAfter);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
