using Demo.Application.Common;
using Demo.Domain.Common;
using Demo.Domain.Modules.ExchangeRates;

namespace Demo.Application.Modules.ExchangeRates;

public static class ExchangeRateErrors
{
    public static Error UnsupportedCurrency(Currency currency, IEnumerable<Currency> supported) =>
        Error.Validation(
            "currency.unsupported",
            $"Currency '{currency}' is not supported. Supported currencies: {FormatList(supported)}.");

    public static Error UnsupportedConversion(Currency from, Currency to, Currency baseCurrency) =>
        Error.Validation(
            "conversion.unsupported",
            $"Conversion from {from} to {to} is not supported; conversions must start from {baseCurrency}.");

    public static Error RateUnavailable(CurrencyPair pair) =>
        Error.Unavailable(
            "exchange_rate.unavailable",
            $"No exchange rate is available yet for {pair}. Rates are synchronized weekly; try again later.");

    public static Error NoRatesApplied(IEnumerable<Currency> missing) =>
        Error.Unavailable(
            "exchange_rate.no_rates_applied",
            $"The provider returned no usable rate for: {FormatList(missing)}.");

    private static string FormatList(IEnumerable<Currency> currencies) => string.Join(", ", currencies.Select(c => c.Code));
}
