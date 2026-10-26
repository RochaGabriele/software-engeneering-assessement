using System.Net;
using System.Text.Json;
using Demo.Domain.Modules.Inventory;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.IntegrationTests.Api;

public class ProductsEndpointTests : IAsyncLifetime
{
    private const string UsdCatalog =
        """[{"sku":"SKU1","name":"Name","price":103.30,"currency":"USD"},{"sku":"SKU2","name":"Name","price":102.20,"currency":"USD"}]""";

    // 103.30 × 0.92 = 95.036 → 95.04; 102.20 × 0.92 = 94.024 → 94.02.
    private const string EurCatalog =
        """[{"sku":"SKU1","name":"Name","price":95.04,"currency":"EUR"},{"sku":"SKU2","name":"Name","price":94.02,"currency":"EUR"}]""";

    private readonly DemoApiFactory _factory = new();
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetCatalog_WithoutCurrency_ReturnsTheWholeCatalogInUsd()
    {
        var response = await _client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UsdCatalog, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("")]
    public async Task GetCatalog_ExplicitUsdOrEmptyCurrency_ReturnsBasePrices(string currency)
    {
        var response = await _client.GetAsync($"/api/products?currency={currency}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(UsdCatalog, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("eur")]
    public async Task GetCatalog_SupportedCurrency_ConvertsWithTheLatestStoredRate(string currency)
    {
        await _factory.StoreRateAsync("EUR", 0.92m);

        var response = await _client.GetAsync($"/api/products?currency={currency}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(EurCatalog, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetCatalog_MalformedCurrency_Returns400Problem()
    {
        var problem = await GetProblemAsync("/api/products?currency=EURO", HttpStatusCode.BadRequest);

        Assert.Equal("currency.invalid", problem.GetProperty("code").GetString());
        Assert.Contains("USD, EUR, CAD, GBP, CHF", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task GetCatalog_UnsupportedCurrency_Returns400Problem()
    {
        var problem = await GetProblemAsync("/api/products?currency=JPY", HttpStatusCode.BadRequest);

        Assert.Equal("currency.unsupported", problem.GetProperty("code").GetString());
        Assert.Contains("USD, EUR, CAD, GBP, CHF", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task GetCatalog_SupportedCurrencyWithoutStoredRate_Returns503Problem()
    {
        var problem = await GetProblemAsync("/api/products?currency=CAD", HttpStatusCode.ServiceUnavailable);

        Assert.Equal("exchange_rate.unavailable", problem.GetProperty("code").GetString());
        Assert.Contains("USD/CAD", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task GetBySku_KnownSku_ReturnsTheProductWithItsCurrency()
    {
        var response = await _client.GetAsync("/api/products/SKU1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"sku":"SKU1","name":"Name","price":103.30,"currency":"USD"}""", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetBySku_UnknownSku_Returns404ProblemWithoutInternals()
    {
        var problem = await GetProblemAsync("/api/products/NOPE", HttpStatusCode.NotFound);

        Assert.Equal("product.not_found", problem.GetProperty("code").GetString());
        Assert.DoesNotContain("stackTrace", problem.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", problem.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnhandledException_Returns500ProblemWithoutInternals()
    {
        await using var failingApi = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IProductRepository, ThrowingProductRepository>()));
        using var client = failingApi.CreateClient();

        var response = await client.GetAsync("/api/products");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("traceId", body);
        Assert.DoesNotContain("boom", body);
        Assert.DoesNotContain("at Demo.", body);
    }

    private async Task<JsonElement> GetProblemAsync(string url, HttpStatusCode expectedStatus)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var problem = document.RootElement.Clone();
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("traceId", out _));
        return problem;
    }

    private sealed class ThrowingProductRepository : IProductRepository
    {
        public Task<Product?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken) => throw new InvalidOperationException("boom");

        public Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }
}
