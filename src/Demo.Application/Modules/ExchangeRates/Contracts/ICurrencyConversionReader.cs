using Demo.Application.Common;
using Demo.Domain.Common;

namespace Demo.Application.Modules.ExchangeRates.Contracts;

/// <summary>
/// Read contract published by the Exchange Rates context. Other contexts (e.g. Inventory) depend on this,
/// never on the exchange-rate aggregate or its repositories (ADR-0002).
/// </summary>
public interface ICurrencyConversionReader
{
    /// <summary>The base currency followed by the configured target currencies.</summary>
    IReadOnlyCollection<Currency> SupportedCurrencies { get; }

    Task<Result<CurrencyConversion>> GetConversionAsync(Currency from, Currency to, CancellationToken cancellationToken);
}
