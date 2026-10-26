using Demo.Domain.Common;

namespace Demo.Application.Modules.ExchangeRates.Ports;

/// <param name="Rates">Units of each quote currency per 1 <paramref name="Base"/>; only well-formed, positive entries.</param>
public sealed record LatestRates(Currency Base, DateTimeOffset PublishedAt, IReadOnlyDictionary<Currency, decimal> Rates);
