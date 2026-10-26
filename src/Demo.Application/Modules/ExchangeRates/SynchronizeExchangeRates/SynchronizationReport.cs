using Demo.Domain.Common;

namespace Demo.Application.Modules.ExchangeRates.SynchronizeExchangeRates;

/// <param name="Applied">Rates created or updated (one history entry each).</param>
/// <param name="Unchanged">Rates whose stored publication is already as recent as the provider's.</param>
/// <param name="Missing">Currencies the provider did not return with a usable rate.</param>
public sealed record SynchronizationReport(
    DateTimeOffset PublishedAt,
    IReadOnlyList<Currency> Applied,
    IReadOnlyList<Currency> Unchanged,
    IReadOnlyList<Currency> Missing);
