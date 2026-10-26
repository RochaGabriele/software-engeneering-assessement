using Demo.Domain.Modules.ExchangeRates;
using Demo.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Demo.Infrastructure.Persistence.Configurations;

internal sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("ExchangeRates");

        // One row per currency pair: the current state (ADR-0001).
        builder.HasKey(rate => new { rate.BaseCurrency, rate.QuoteCurrency });
        builder.Property(rate => rate.BaseCurrency).HasConversion(new CurrencyConverter()).HasMaxLength(3);
        builder.Property(rate => rate.QuoteCurrency).HasConversion(new CurrencyConverter()).HasMaxLength(3);
        builder.Property(rate => rate.Rate).HasPrecision(18, 8);
        builder.Property(rate => rate.PublishedAt).HasConversion(new UnixMillisecondsConverter());
        builder.Property(rate => rate.RetrievedAt).HasConversion(new UnixMillisecondsConverter());

        builder.Ignore(rate => rate.Pair);
    }
}
