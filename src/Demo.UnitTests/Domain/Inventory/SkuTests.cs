using Demo.Domain.Common;
using Demo.Domain.Modules.Inventory;

namespace Demo.UnitTests.Domain.Inventory;

public class SkuTests
{
    [Fact]
    public void Create_TrimsValue()
    {
        Assert.Equal("SKU1", Sku.Create(" SKU1 ").Value);
    }

    [Fact]
    public void Create_AtMaxLength_Succeeds()
    {
        Assert.Equal(Sku.MaxLength, Sku.Create(new string('X', Sku.MaxLength)).Value.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Blank_ThrowsDomainException(string value)
    {
        Assert.Throws<DomainException>(() => Sku.Create(value));
    }

    [Fact]
    public void Create_TooLong_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => Sku.Create(new string('X', Sku.MaxLength + 1)));
    }

    [Fact]
    public void TryCreate_Invalid_ReturnsFalse()
    {
        Assert.False(Sku.TryCreate(null, out var missing));
        Assert.Null(missing);
        Assert.False(Sku.TryCreate(new string('X', Sku.MaxLength + 1), out _));
    }

    [Fact]
    public void TryCreate_Valid_ReturnsTrimmedSku()
    {
        Assert.True(Sku.TryCreate(" SKU2", out var sku));
        Assert.Equal(Sku.Create("SKU2"), sku);
    }
}
