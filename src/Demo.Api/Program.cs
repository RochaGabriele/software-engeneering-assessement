using Demo.Application;
using Demo.Infrastructure;
using Demo.Infrastructure.Persistence;
using Hangfire;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
// Unhandled exceptions and empty error responses become RFC 9457 ProblemDetails, without stack traces.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Add Hangfire Dashboard
app.UseHangfireDashboard();

app.MapControllers();

// Apply migrations; demo data is seeded by the UseSeeding hook configured in AddInfrastructure.
await app.Services.MigrateDatabaseAsync();

await app.RunAsync();
