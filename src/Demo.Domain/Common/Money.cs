namespace Demo.Domain.Common;

public sealed record Money
{
    /// <summary>All supported currencies (USD, EUR, CAD, GBP, CHF) have two minor units.</summary>
    public const int MinorUnits = 2;

    public decimal Amount { get; }

    public Currency Currency { get; }

    public Money(decimal amount, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Rounds half away from zero (consumer price convention, not banker's rounding)
    /// and normalizes the scale, so 103.3 is represented and serialized as 103.30.
    /// </summary>
    public Money RoundToMinorUnits() =>
        new(decimal.Round(Amount, MinorUnits, MidpointRounding.AwayFromZero) + 0.00m, Currency);
}
