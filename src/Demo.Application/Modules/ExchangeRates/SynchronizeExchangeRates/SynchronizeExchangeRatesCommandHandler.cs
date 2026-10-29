using Demo.Application.Common;
using Demo.Application.Modules.ExchangeRates.Ports;
using Demo.Domain.Common;
using Demo.Domain.Modules.ExchangeRates;
using Microsoft.Extensions.Logging;

namespace Demo.Application.Modules.ExchangeRates.SynchronizeExchangeRates;

/// <summary>
/// Keeps the current rate per currency up to date and logs every applied observation to the history,
/// committing both in a single unit of work (ADR-0001).
/// </summary>
public sealed class SynchronizeExchangeRatesCommandHandler
    : ICommandHandler<SynchronizeExchangeRatesCommand, Result<SynchronizationReport>>
{
    /// <summary>How far ahead of this server's clock a provider publication time may be (ordinary clock skew).</summary>
    private static readonly TimeSpan ClockSkewTolerance = TimeSpan.FromHours(1);

    private readonly IExchangeRateProvider _provider;
    private readonly IExchangeRateRepository _exchangeRates;
    private readonly IExchangeRateHistoryRepository _history;
    private readonly IUnitOfWork _unitOfWork;
    private readonly SupportedCurrencies _supportedCurrencies;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public SynchronizeExchangeRatesCommandHandler(
        IExchangeRateProvider provider,
        IExchangeRateRepository exchangeRates,
        IExchangeRateHistoryRepository history,
        IUnitOfWork unitOfWork,
        SupportedCurrencies supportedCurrencies,
        TimeProvider timeProvider,
        ILogger<SynchronizeExchangeRatesCommandHandler> logger)
    {
        _provider = provider;
        _exchangeRates = exchangeRates;
        _history = history;
        _unitOfWork = unitOfWork;
        _supportedCurrencies = supportedCurrencies;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<SynchronizationReport>> HandleAsync(SynchronizeExchangeRatesCommand command, CancellationToken cancellationToken)
    {
        var latest = await _provider.GetLatestRatesAsync(_supportedCurrencies.Base, _supportedCurrencies.Targets, cancellationToken);
        var retrievedAt = _timeProvider.GetUtcNow();

        // Rates on another base would be stored as USD→X and corrupt every converted price; never trust the adapter on this.
        if (latest.Base != _supportedCurrencies.Base)
        {
            return ExchangeRateErrors.UnexpectedBase(_supportedCurrencies.Base, latest.Base);
        }

        // A future publication time, once stored, would make every real publication look older and freeze the rates.
        if (latest.PublishedAt > retrievedAt + ClockSkewTolerance)
        {
            return ExchangeRateErrors.PublicationInTheFuture(latest.PublishedAt, retrievedAt);
        }

        var applied = new List<Currency>();
        var unchanged = new List<Currency>();
        var missing = new List<Currency>();

        foreach (var target in _supportedCurrencies.Targets)
        {
            // A partial answer must not block the currencies that did arrive (DR-07).
            if (!latest.Rates.TryGetValue(target, out var value) || value <= 0)
            {
                _logger.LogWarning("Exchange rate provider returned no usable rate for {Currency}.", target.Code);
                missing.Add(target);
                continue;
            }

            var pair = new CurrencyPair(_supportedCurrencies.Base, target);
            var exchangeRate = await _exchangeRates.GetAsync(pair, cancellationToken);

            if (exchangeRate is null)
            {
                exchangeRate = ExchangeRate.Create(pair, value, latest.PublishedAt, retrievedAt);
                _exchangeRates.Add(exchangeRate);
            }
            else if (!exchangeRate.Update(value, latest.PublishedAt, retrievedAt))
            {
                unchanged.Add(target);
                continue;
            }

            _history.Add(ExchangeRateHistoryEntry.Record(exchangeRate));
            applied.Add(target);
        }

        if (applied.Count == 0 && unchanged.Count == 0)
        {
            return ExchangeRateErrors.NoRatesApplied(missing);
        }

        if (applied.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Exchange rates published at {PublishedAt:O} synchronized. Applied: {Applied}; unchanged: {Unchanged}; missing: {Missing}.",
            latest.PublishedAt,
            applied.Count,
            unchanged.Count,
            missing.Count);

        return Result<SynchronizationReport>.Success(new SynchronizationReport(latest.PublishedAt, applied, unchanged, missing));
    }
}
