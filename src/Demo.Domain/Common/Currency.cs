using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Demo.Domain.Common;

/// <summary>An ISO 4217 currency, identified by its three-letter code (e.g. USD).</summary>
public sealed record Currency
{
    private const int CodeLength = 3;
    private const int DefaultMinorUnits = 2;

    // ISO 4217 currencies whose minor unit is not 2 (list one, published by SIX); every other code uses 2.
    private static readonly FrozenDictionary<string, int> NonDefaultMinorUnits = new Dictionary<string, int>
    {
        ["BIF"] = 0, ["CLP"] = 0, ["DJF"] = 0, ["GNF"] = 0, ["ISK"] = 0, ["JPY"] = 0, ["KMF"] = 0, ["KRW"] = 0, ["PYG"] = 0,
        ["RWF"] = 0, ["UGX"] = 0, ["UYI"] = 0, ["VND"] = 0, ["VUV"] = 0, ["XAF"] = 0, ["XOF"] = 0, ["XPF"] = 0,
        ["BHD"] = 3, ["IQD"] = 3, ["JOD"] = 3, ["KWD"] = 3, ["LYD"] = 3, ["OMR"] = 3, ["TND"] = 3,
        ["CLF"] = 4, ["UYW"] = 4
    }.ToFrozenDictionary();

    public static readonly Currency USD = new("USD");

    public string Code { get; }

    /// <summary>Decimal places of the currency's minor unit (cents): 2 for USD/EUR, 0 for JPY, 3 for KWD.</summary>
    public int MinorUnits => NonDefaultMinorUnits.GetValueOrDefault(Code, DefaultMinorUnits);

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
