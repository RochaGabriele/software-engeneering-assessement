namespace Demo.Domain.Common;

/// <summary>Converts money from one currency to another at a given rate (1 <see cref="From"/> = <see cref="Rate"/> <see cref="To"/>).</summary>
public sealed record CurrencyConversion
{
    public Currency From { get; }

    public Currency To { get; }

    public decimal Rate { get; }

    /// <summary>When the rate was published by its source; <c>null</c> for an identity conversion.</summary>
    public DateTimeOffset? AsOf { get; }

    public CurrencyConversion(Currency from, Currency to, decimal rate, DateTimeOffset? asOf)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (rate <= 0)
        {
            throw new DomainException($"An exchange rate must be positive, but was {rate}.");
        }

        From = from;
        To = to;
        Rate = rate;
        AsOf = asOf;
    }

    public static CurrencyConversion Identity(Currency currency) => new(currency, currency, 1m, asOf: null);

    /// <summary>Multiplies by the full-precision rate and rounds once, at the end.</summary>
    public Money Convert(Money money)
    {
        if (money.Currency != From)
        {
            throw new DomainException($"Cannot convert {money.Currency} with a {From} to {To} conversion.");
        }

        return new Money(money.Amount * Rate, To).RoundToMinorUnits();
    }
}
