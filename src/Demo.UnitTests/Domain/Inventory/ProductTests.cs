using Demo.Domain.Common;
using Demo.Domain.Modules.Inventory;

namespace Demo.UnitTests.Domain.Inventory;

public class ProductTests
{
    private static readonly Sku Sku1 = Sku.Create("SKU1");

    [Fact]
    public void Create_ValidProduct_SetsProperties()
    {
        var product = Product.Create(Sku1, " Name ", new Money(103.30m, Currency.USD));

        Assert.Equal(Sku1, product.Sku);
        Assert.Equal("Name", product.Name);
        Assert.Equal(new Money(103.30m, Currency.USD), product.Price);
    }

    [Fact]
    public void Create_ZeroPrice_IsAllowed()
    {
        Assert.Equal(0m, Product.Create(Sku1, "Free sample", new Money(0m, Currency.USD)).Price.Amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_ThrowsDomainException(string name)
    {
        Assert.Throws<DomainException>(() => Product.Create(Sku1, name, new Money(1m, Currency.USD)));
    }

    [Fact]
    public void Create_NameTooLong_ThrowsDomainException()
    {
        var name = new string('n', Product.NameMaxLength + 1);

        Assert.Throws<DomainException>(() => Product.Create(Sku1, name, new Money(1m, Currency.USD)));
    }

    [Fact]
    public void Create_NegativePrice_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Product.Create(Sku1, "Name", new Money(-0.01m, Currency.USD)));
    }

    [Fact]
    public void Create_PriceNotInBaseCurrency_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Product.Create(Sku1, "Name", new Money(10m, Currency.Create("EUR"))));
    }
}
