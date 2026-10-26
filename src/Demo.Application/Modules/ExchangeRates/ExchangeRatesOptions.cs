namespace Demo.Application.Modules.ExchangeRates;

public sealed class ExchangeRatesOptions
{
    public const string SectionName = "ExchangeRates";

    // No default values here: configuration binding appends to an initialized collection,
    // so the defaults live in appsettings.json.
    public string[] TargetCurrencies { get; init; } = [];
}
