using Demo.Domain.Modules.ExchangeRates;
using Demo.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Demo.Infrastructure.Persistence.Configurations;

internal sealed class ExchangeRateHistoryEntryConfiguration : IEntityTypeConfiguration<ExchangeRateHistoryEntry>
{
    public void Configure(EntityTypeBuilder<ExchangeRateHistoryEntry> builder)
    {
        builder.ToTable("ExchangeRateHistory");

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedOnAdd();
        builder.Property(entry => entry.BaseCurrency).HasConversion(new CurrencyConverter()).HasMaxLength(3);
        builder.Property(entry => entry.QuoteCurrency).HasConversion(new CurrencyConverter()).HasMaxLength(3);
        builder.Property(entry => entry.Rate).HasPrecision(18, 8);
        builder.Property(entry => entry.PublishedAt).HasConversion(new UnixMillisecondsConverter());
        builder.Property(entry => entry.RetrievedAt).HasConversion(new UnixMillisecondsConverter());

        // Storage-level guarantee that one provider publication is logged once per pair, even if the job reruns (DR-05).
        builder.HasIndex(entry => new { entry.BaseCurrency, entry.QuoteCurrency, entry.PublishedAt }).IsUnique();
    }
}
