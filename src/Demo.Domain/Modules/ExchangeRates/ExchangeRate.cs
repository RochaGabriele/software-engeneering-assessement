using Demo.Domain.Common;

namespace Demo.Domain.Modules.ExchangeRates;

/// <summary>
/// The latest known rate for a currency pair (aggregate root, identified by the pair).
/// Only observations published after the current one are applied, which makes synchronization idempotent.
/// </summary>
public sealed class ExchangeRate
{
    public Currency BaseCurrency { get; private set; } = null!;

    public Currency QuoteCurrency { get; private set; } = null!;

    public decimal Rate { get; private set; }

    /// <summary>When the provider published the rate; this, not <see cref="RetrievedAt"/>, defines "latest".</summary>
    public DateTimeOffset PublishedAt { get; private set; }

    public DateTimeOffset RetrievedAt { get; private set; }

    public CurrencyPair Pair => new(BaseCurrency, QuoteCurrency);

    private ExchangeRate()
    {
    }

    public static ExchangeRate Create(CurrencyPair pair, decimal rate, DateTimeOffset publishedAt, DateTimeOffset retrievedAt)
    {
        ArgumentNullException.ThrowIfNull(pair);
        EnsurePositive(rate);

        return new ExchangeRate
        {
            BaseCurrency = pair.Base,
            QuoteCurrency = pair.Quote,
            Rate = rate,
            PublishedAt = publishedAt,
            RetrievedAt = retrievedAt
        };
    }

    /// <returns><c>false</c> when <paramref name="publishedAt"/> is not strictly newer (a stale or duplicate observation); the state is then unchanged.</returns>
    public bool Update(decimal rate, DateTimeOffset publishedAt, DateTimeOffset retrievedAt)
    {
        if (publishedAt <= PublishedAt)
        {
            return false;
        }

        EnsurePositive(rate);

        Rate = rate;
        PublishedAt = publishedAt;
        RetrievedAt = retrievedAt;
        return true;
    }

    public CurrencyConversion ToConversion() => new(BaseCurrency, QuoteCurrency, Rate, PublishedAt);

    private static void EnsurePositive(decimal rate)
    {
        if (rate <= 0)
        {
            throw new DomainException($"An exchange rate must be positive, but was {rate}.");
        }
    }
}
