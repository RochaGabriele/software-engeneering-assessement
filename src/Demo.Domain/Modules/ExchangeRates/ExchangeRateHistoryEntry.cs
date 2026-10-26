using Demo.Domain.Common;

namespace Demo.Domain.Modules.ExchangeRates;

/// <summary>Immutable record of a rate observation, appended whenever an <see cref="ExchangeRate"/> is created or updated.</summary>
public sealed class ExchangeRateHistoryEntry
{
    public long Id { get; private set; }

    public Currency BaseCurrency { get; private set; } = null!;

    public Currency QuoteCurrency { get; private set; } = null!;

    public decimal Rate { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    public DateTimeOffset RetrievedAt { get; private set; }

    private ExchangeRateHistoryEntry()
    {
    }

    public static ExchangeRateHistoryEntry Record(ExchangeRate rate)
    {
        ArgumentNullException.ThrowIfNull(rate);

        return new ExchangeRateHistoryEntry
        {
            BaseCurrency = rate.BaseCurrency,
            QuoteCurrency = rate.QuoteCurrency,
            Rate = rate.Rate,
            PublishedAt = rate.PublishedAt,
            RetrievedAt = rate.RetrievedAt
        };
    }
}
