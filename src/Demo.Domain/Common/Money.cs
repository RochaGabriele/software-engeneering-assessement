namespace Demo.Domain.Common;

public sealed record Money
{
    public decimal Amount { get; }

    public Currency Currency { get; }

    public Money(decimal amount, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Rounds to the currency's minor units, half away from zero (consumer price convention, not banker's rounding),
    /// and normalizes the scale to exactly that many decimals, so 103.3 USD is represented and serialized as 103.30.
    /// </summary>
    public Money RoundToMinorUnits()
    {
        var decimals = Currency.MinorUnits;
        var rounded = decimal.Round(Amount, decimals, MidpointRounding.AwayFromZero);

        // Adding a zero with the target scale pads the result to that scale (decimal addition keeps the larger scale).
        var zeroWithTargetScale = new decimal(0, 0, 0, isNegative: false, scale: (byte)decimals);
        return new Money(rounded + zeroWithTargetScale, Currency);
    }
}
