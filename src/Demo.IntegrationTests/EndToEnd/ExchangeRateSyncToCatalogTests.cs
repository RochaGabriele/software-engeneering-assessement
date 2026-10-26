using System.Net;
using Demo.Application.Modules.ExchangeRates.Ports;
using Demo.Domain.Common;
using Demo.Infrastructure.BackgroundJobs;
using Demo.Infrastructure.Persistence;
using Demo.IntegrationTests.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.IntegrationTests.EndToEnd;

/// <summary>
/// The whole Task 2 → Task 3 path with only the external provider replaced: the Hangfire job class, the sync handler,
/// EF Core over SQLite and the HTTP pipeline are all the real ones.
/// </summary>
public class ExchangeRateSyncToCatalogTests : IAsyncLifetime
{
    private readonly DemoApiFactory _factory = new();
    private WebApplicationFactory<Program> _api = null!;

    public Task InitializeAsync()
    {
        _api = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IExchangeRateProvider>(new FixedRatesProvider())));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _api.DisposeAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Sync_MakesEveryCurrencyAvailableToTheCatalog_AndRerunningIsIdempotent()
    {
        using var client = _api.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/products?currency=EUR")).StatusCode);

        await RunSyncJobAsync();

        await AssertCatalogAsync(client, "EUR", "95.04", "94.02");
        await AssertCatalogAsync(client, "CAD", "144.62", "143.08");
        await AssertCatalogAsync(client, "GBP", "77.48", "76.65");
        await AssertCatalogAsync(client, "CHF", "82.64", "81.76");
        Assert.Equal(4, await CountHistoryAsync());

        // Same provider publication again (manual trigger, retry, bootstrap...): nothing new is logged (DR-05).
        await RunSyncJobAsync();

        Assert.Equal(4, await CountHistoryAsync());
    }

    private async Task RunSyncJobAsync()
    {
        await using var scope = _api.Services.CreateAsyncScope();
        var job = ActivatorUtilities.CreateInstance<ExchangeRatesSyncJob>(scope.ServiceProvider);

        await job.ExecuteAsync(CancellationToken.None);
    }

    private async Task<int> CountHistoryAsync()
    {
        await using var scope = _api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DemoDbContext>().ExchangeRateHistory.CountAsync();
    }

    private static async Task AssertCatalogAsync(HttpClient client, string currency, string sku1Price, string sku2Price)
    {
        var response = await client.GetAsync($"/api/products?currency={currency}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            $$"""[{"sku":"SKU1","name":"Name","price":{{sku1Price}},"currency":"{{currency}}"},{"sku":"SKU2","name":"Name","price":{{sku2Price}},"currency":"{{currency}}"}]""",
            await response.Content.ReadAsStringAsync());
    }

    private sealed class FixedRatesProvider : IExchangeRateProvider
    {
        public Task<LatestRates> GetLatestRatesAsync(Currency baseCurrency, IReadOnlyCollection<Currency> targets, CancellationToken cancellationToken) =>
            Task.FromResult(new LatestRates(
                baseCurrency,
                DemoApiFactory.RatesPublishedAt,
                new Dictionary<Currency, decimal>
                {
                    [Currency.Create("EUR")] = 0.92m,
                    [Currency.Create("CAD")] = 1.40m,
                    [Currency.Create("GBP")] = 0.75m,
                    [Currency.Create("CHF")] = 0.80m
                }));
    }
}
