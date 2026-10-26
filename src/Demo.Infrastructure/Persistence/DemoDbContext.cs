using Demo.Domain.Modules.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Demo.Infrastructure.Persistence;

public class DemoDbContext : DbContext
{
    public DbSet<Product> Products { get; set; } = null!;

    public DemoDbContext(DbContextOptions<DemoDbContext> options) : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(b =>
        {
            b.HasKey(p => p.Sku);
            b.Property(p => p.Name).IsRequired().HasMaxLength(200);
            b.Property(p => p.Price).HasColumnType("decimal(18,2)");
        });
    }
}
