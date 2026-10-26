using System.Diagnostics.CodeAnalysis;

namespace Demo.Domain.Common;

/// <summary>An ISO 4217 currency, identified by its three-letter code (e.g. USD).</summary>
public sealed record Currency
{
    private const int CodeLength = 3;

    public static readonly Currency USD = new("USD");

    public string Code { get; }

    private Currency(string code) => Code = code;

    public static Currency Create(string code) =>
        TryParse(code, out var currency)
            ? currency
            : throw new DomainException($"'{code}' is not a valid ISO 4217 currency code.");

    /// <summary>Accepts surrounding whitespace and any letter case; normalizes the code to upper case.</summary>
    public static bool TryParse(string? value, [NotNullWhen(true)] out Currency? currency)
    {
        currency = null;

        var code = value?.Trim();
        if (code is not { Length: CodeLength } || !code.All(char.IsAsciiLetter))
        {
            return false;
        }

        currency = new Currency(code.ToUpperInvariant());
        return true;
    }

    public override string ToString() => Code;
}
