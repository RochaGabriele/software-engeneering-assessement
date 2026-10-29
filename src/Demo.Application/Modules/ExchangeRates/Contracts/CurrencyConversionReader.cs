using Demo.Application.Common;
using Demo.Domain.Common;
using Demo.Domain.Modules.ExchangeRates;

namespace Demo.Application.Modules.ExchangeRates.Contracts;

public sealed class CurrencyConversionReader : ICurrencyConversionReader
{
    // Qualified: inside this class, "SupportedCurrencies" names the contract's property, not the type.
    private static readonly Currency BaseCurrency = ExchangeRates.SupportedCurrencies.BaseCurrency;

    private readonly IExchangeRateRepository _exchangeRates;
    private readonly SupportedCurrencies _supportedCurrencies;

    public CurrencyConversionReader(IExchangeRateRepository exchangeRates, SupportedCurrencies supportedCurrencies)
    {
        _exchangeRates = exchangeRates;
        _supportedCurrencies = supportedCurrencies;
    }

    public IReadOnlyCollection<Currency> SupportedCurrencies => _supportedCurrencies.All;

    public async Task<Result<CurrencyConversion>> GetConversionAsync(Currency from, Currency to, CancellationToken cancellationToken)
    {
        foreach (var currency in new[] { from, to })
        {
            if (!_supportedCurrencies.IsSupported(currency))
            {
                return ExchangeRateErrors.UnsupportedCurrency(currency, _supportedCurrencies.All);
            }
        }

        if (from == to)
        {
            return Result<CurrencyConversion>.Success(CurrencyConversion.Identity(from));
        }

        // Rates are stored against the base only; cross rates (e.g. EUR to CAD) are not offered.
        if (from != BaseCurrency)
        {
            return ExchangeRateErrors.UnsupportedConversion(from, to, BaseCurrency);
        }

        var pair = new CurrencyPair(from, to);
        var exchangeRate = await _exchangeRates.GetAsync(pair, cancellationToken);

        return exchangeRate is null
            ? ExchangeRateErrors.RateUnavailable(pair)
            : Result<CurrencyConversion>.Success(exchangeRate.ToConversion());
    }
}
