using Demo.Application.Modules;
using Demo.Domain.Common.Services;
using Demo.Domain.Modules.Inventory;
using Demo.Infrastructure;
using Demo.Infrastructure.Messaging;
using Demo.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Setup Database
builder.Services.AddDbContext<DemoDbContext>(options =>
    options.UseSqlite("Data Source=demo.db"));

// Setup Repositories and Infra
builder.Services.AddTransient<IProductRepository, ProductRepository>();
builder.Services.AddTransient<IMessagePublisher, FakeMessagePublisher>();

// Setup Modules
builder.Services.AddTransient<IInventoryModule, InventoryModule>();

// Setup Hangfire via Infrastructure
builder.Services.AddInfrastructureHangfire();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Add Hangfire Dashboard
app.UseHangfireDashboard();

app.MapControllers();

// Seed Database for Testing
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DemoDbContext>();
    db.Database.EnsureCreated();

    if (!db.Products.Any())
    {
        db.Products.Add(
            new Product()
            {
                Sku = "SKU1",
                Name = "Name",
                Price = 103.30M
            }
        );

        db.Products.Add(
            new Product()
            {
                Sku = "SKU2",
                Name = "Name",
                Price = 102.20M
            }
        );

        db.SaveChanges();
    }
}

app.Run();
