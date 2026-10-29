namespace Demo.Infrastructure.ExchangeRates.OpenExchangeRates;

// Provider wire formats (https://docs.openexchangerates.org/reference/latest-json and /errors).
// They never leave this adapter. Rates are decimal, never double, so no binary rounding enters the system.
// Fields are nullable so a missing or null value is reported as unusable instead of silently becoming 0.

internal sealed record LatestRatesResponse(long? Timestamp, string? Base, Dictionary<string, decimal?>? Rates);

internal sealed record ErrorResponse(bool Error, int Status, string? Message, string? Description);
