using Demo.Domain.Modules.Inventory;
using Demo.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Demo.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(product => product.Sku);
        builder.Property(product => product.Sku).HasConversion(new SkuConverter()).HasMaxLength(Sku.MaxLength);
        builder.Property(product => product.Name).IsRequired().HasMaxLength(Product.NameMaxLength);

        builder.ComplexProperty(product => product.Price, price =>
        {
            // HasPrecision, not HasColumnType("decimal(18,2)"): in SQLite that type name gets NUMERIC affinity
            // and silently stores a binary floating-point REAL. With HasPrecision EF stores exact TEXT (Task 1, F-02).
            price.Property(money => money.Amount).HasColumnName("Price").HasPrecision(18, 2);
            price.Property(money => money.Currency).HasColumnName("PriceCurrency").HasConversion(new CurrencyConverter()).HasMaxLength(3);
        });
    }
}
