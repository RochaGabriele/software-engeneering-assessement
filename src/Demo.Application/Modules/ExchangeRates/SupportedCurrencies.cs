using Demo.Domain.Common;

namespace Demo.Application.Modules.ExchangeRates;

/// <summary>Single source of truth for the currencies the sync fetches and the catalog accepts.</summary>
public sealed class SupportedCurrencies
{
    /// <summary>
    /// USD: the only base the Open Exchange Rates free plan offers, and the currency product prices are stored in.
    /// </summary>
    public Currency Base { get; } = Currency.USD;

    public IReadOnlyList<Currency> Targets { get; }

    public IReadOnlyList<Currency> All { get; }

    public SupportedCurrencies(IEnumerable<Currency> targets)
    {
        Targets = [.. targets];
        All = [Base, .. Targets];
    }

    /// <remarks>Expects options already checked by <see cref="ExchangeRatesOptionsValidator"/>.</remarks>
    public static SupportedCurrencies From(ExchangeRatesOptions options) =>
        new(options.TargetCurrencies.Select(Currency.Create));

    public bool IsSupported(Currency currency) => All.Contains(currency);
}
