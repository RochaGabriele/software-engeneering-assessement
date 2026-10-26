using Demo.Domain.Common;

namespace Demo.Domain.Modules.ExchangeRates;

/// <summary>A directed currency pair: 1 <see cref="Base"/> is quoted in <see cref="Quote"/>.</summary>
public sealed record CurrencyPair
{
    public Currency Base { get; }

    public Currency Quote { get; }

    public CurrencyPair(Currency @base, Currency quote)
    {
        ArgumentNullException.ThrowIfNull(@base);
        ArgumentNullException.ThrowIfNull(quote);

        if (@base == quote)
        {
            throw new DomainException($"A currency pair needs two different currencies, but both were {@base}.");
        }

        Base = @base;
        Quote = quote;
    }

    public override string ToString() => $"{Base}/{Quote}";
}
