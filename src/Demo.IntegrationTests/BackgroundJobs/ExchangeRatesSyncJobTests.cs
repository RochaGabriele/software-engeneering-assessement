using System.Reflection;
using Demo.Application.Common;
using Demo.Application.Modules.ExchangeRates;
using Demo.Application.Modules.ExchangeRates.SynchronizeExchangeRates;
using Demo.Domain.Common;
using Demo.Infrastructure.BackgroundJobs;
using Hangfire;

namespace Demo.IntegrationTests.BackgroundJobs;

public class ExchangeRatesSyncJobTests
{
    [Fact]
    public async Task Execute_SuccessfulSync_Completes()
    {
        var report = new SynchronizationReport(DateTimeOffset.UnixEpoch, [Currency.Create("EUR")], [], []);
        var handler = new HandlerStub(Result<SynchronizationReport>.Success(report));

        await new ExchangeRatesSyncJob(handler).ExecuteAsync(CancellationToken.None);

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Execute_FailedSync_ThrowsSoHangfireRecordsAndRetriesTheRun()
    {
        var error = ExchangeRateErrors.NoRatesApplied([Currency.Create("EUR")]);
        var job = new ExchangeRatesSyncJob(new HandlerStub(error));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => job.ExecuteAsync(CancellationToken.None));

        Assert.Equal(error.Message, exception.Message);
    }

    [Fact]
    public async Task Execute_ForwardsTheJobCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new HandlerStub(Result<SynchronizationReport>.Success(new SynchronizationReport(DateTimeOffset.UnixEpoch, [], [], [])));

        await new ExchangeRatesSyncJob(handler).ExecuteAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, handler.ReceivedToken);
    }

    [Fact]
    public void Execute_NeverOverlaps()
    {
        var method = typeof(ExchangeRatesSyncJob).GetMethod(nameof(ExchangeRatesSyncJob.ExecuteAsync))!;

        Assert.NotNull(method.GetCustomAttribute<DisableConcurrentExecutionAttribute>());
    }

    [Fact]
    public void Execute_RetriesCoverAProviderOutageOfHoursNotMinutes()
    {
        // Hangfire's default backoff spends 3 attempts in about 4 minutes; a weekly job needs a much wider window.
        var retry = typeof(ExchangeRatesSyncJob).GetMethod(nameof(ExchangeRatesSyncJob.ExecuteAsync))!
            .GetCustomAttribute<AutomaticRetryAttribute>()!;

        Assert.NotNull(retry.DelaysInSeconds);
        Assert.Equal(retry.Attempts, retry.DelaysInSeconds.Length);
        Assert.True(TimeSpan.FromSeconds(retry.DelaysInSeconds.Sum()) >= TimeSpan.FromHours(4));
    }

    private sealed class HandlerStub(Result<SynchronizationReport> result)
        : ICommandHandler<SynchronizeExchangeRatesCommand, Result<SynchronizationReport>>
    {
        public int CallCount { get; private set; }

        public CancellationToken ReceivedToken { get; private set; }

        public Task<Result<SynchronizationReport>> HandleAsync(SynchronizeExchangeRatesCommand command, CancellationToken cancellationToken)
        {
            CallCount++;
            ReceivedToken = cancellationToken;
            return Task.FromResult(result);
        }
    }
}
