using Demo.Application;
using Demo.Domain.Modules.Inventory;
using Demo.Infrastructure;
using Demo.Infrastructure.Persistence;
using Hangfire;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

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
