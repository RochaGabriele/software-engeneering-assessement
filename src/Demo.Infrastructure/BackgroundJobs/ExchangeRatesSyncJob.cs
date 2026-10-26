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

    // Overlapping runs (schedule, bootstrap, manual trigger) are serialized; failed runs are retried with
    // Hangfire's increasing delays, on top of the HTTP-level retries for short blips (DR-11, DR-12).
    [JobDisplayName("Synchronize exchange rates")]
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    [AutomaticRetry(Attempts = 3)]
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
