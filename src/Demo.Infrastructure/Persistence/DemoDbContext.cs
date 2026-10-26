using Demo.Application.Common;
using Demo.Domain.Modules.ExchangeRates;
using Demo.Domain.Modules.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Demo.Infrastructure.Persistence;

public class DemoDbContext : DbContext, IUnitOfWork
{
    public DbSet<Product> Products { get; set; } = null!;

    public DbSet<ExchangeRate> ExchangeRates { get; set; } = null!;

    public DbSet<ExchangeRateHistoryEntry> ExchangeRateHistory { get; set; } = null!;

    public DemoDbContext(DbContextOptions<DemoDbContext> options) : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DemoDbContext).Assembly);
    }
}
