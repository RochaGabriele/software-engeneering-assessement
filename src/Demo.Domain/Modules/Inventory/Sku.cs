using System.Diagnostics.CodeAnalysis;
using Demo.Domain.Common;

namespace Demo.Domain.Modules.Inventory;

/// <summary>Stock keeping unit: the product's identity. Surrounding whitespace is not significant.</summary>
public sealed record Sku
{
    public const int MaxLength = 64;

    public string Value { get; }

    private Sku(string value) => Value = value;

    public static Sku Create(string value) =>
        TryCreate(value, out var sku)
            ? sku
            : throw new DomainException($"A SKU must have between 1 and {MaxLength} characters, but was '{value}'.");

    public static bool TryCreate(string? value, [NotNullWhen(true)] out Sku? sku)
    {
        var trimmed = value?.Trim();

        sku = string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength ? null : new Sku(trimmed);
        return sku is not null;
    }

    public override string ToString() => Value;
}
