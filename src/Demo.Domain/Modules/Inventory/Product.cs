using Demo.Domain.Common;

namespace Demo.Domain.Modules.Inventory;

/// <summary>A catalog product (aggregate root, identified by its <see cref="Sku"/>).</summary>
public sealed class Product
{
    public const int NameMaxLength = 200;

    /// <summary>
    /// Product prices are stored in USD; other currencies are derived through exchange rates, which must therefore use the
    /// same base (the Exchange Rates context's own definition; a test pins the two together).
    /// </summary>
    public static readonly Currency BaseCurrency = Currency.USD;

    public Sku Sku { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public Money Price { get; private set; } = null!;

    private Product()
    {
    }

    public static Product Create(Sku sku, string name, Money price)
    {
        ArgumentNullException.ThrowIfNull(sku);
        ArgumentNullException.ThrowIfNull(price);

        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > NameMaxLength)
        {
            throw new DomainException($"A product name must have between 1 and {NameMaxLength} characters.");
        }

        if (price.Amount < 0)
        {
            throw new DomainException($"A product price cannot be negative, but was {price.Amount}.");
        }

        if (price.Currency != BaseCurrency)
        {
            throw new DomainException($"Product prices must be in {BaseCurrency}, but was {price.Currency}.");
        }

        return new Product { Sku = sku, Name = trimmedName, Price = price };
    }
}
