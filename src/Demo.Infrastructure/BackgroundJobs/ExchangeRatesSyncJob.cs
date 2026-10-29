using Demo.Application.Common;
using Demo.Application.Modules.ExchangeRates.SynchronizeExchangeRates;
using Hangfire;

namespace Demo.Infrastructure.BackgroundJobs;

/// <summary>Thin Hangfire entry point; the business logic lives in the application handler (ADR-0005).</summary>
public sealed class ExchangeRatesSyncJob
{
    public const string RecurringJobId = "exchange-rates-sync";

    private readonly ICommandHandler<SynchronizeExchangeRatesCommand, Result<SynchronizationReport>> _handler;

    public ExchangeRatesSyncJob(ICommandHandler<SynchronizeExchangeRatesCommand, Result<SynchronizationReport>> handler)
    {
        _handler = handler;
    }

    // Overlapping runs (schedule, startup catch-up, manual trigger) are serialized (DR-11). The HTTP pipeline absorbs
    // blips of seconds; these job-level retries (1 min, 5 min, 15 min, 1 h, 3 h) ride out a provider outage of about
    // 4 hours, where Hangfire's default backoff would give up after about 4 minutes and lose the week (DR-12).
    [JobDisplayName("Synchronize exchange rates")]
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = new[] { 60, 300, 900, 3600, 10800 })]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await _handler.HandleAsync(new SynchronizeExchangeRatesCommand(), cancellationToken);

        // A failed result must fail the job, otherwise Hangfire would mark the run as succeeded and never retry it.
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.Error!.Message);
        }
    }
}
