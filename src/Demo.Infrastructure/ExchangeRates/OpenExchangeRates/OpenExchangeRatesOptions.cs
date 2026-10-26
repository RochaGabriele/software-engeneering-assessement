namespace Demo.Infrastructure.ExchangeRates.OpenExchangeRates;

public sealed class OpenExchangeRatesOptions
{
    public const string SectionName = "OpenExchangeRates";

    public string BaseUrl { get; init; } = "https://openexchangerates.org/api/";

    /// <summary>
    /// Secret. Supplied through user-secrets or the <c>OpenExchangeRates__AppId</c> environment variable,
    /// never through a committed settings file (ADR-0007).
    /// </summary>
    public string? AppId { get; init; }
}
