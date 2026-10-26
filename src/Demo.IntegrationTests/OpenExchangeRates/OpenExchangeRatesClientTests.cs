using System.Net;
using System.Text.Json;
using Demo.Application.Modules.ExchangeRates.Ports;
using Demo.Domain.Common;
using Demo.Infrastructure.ExchangeRates.OpenExchangeRates;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Demo.IntegrationTests.OpenExchangeRates;

public class OpenExchangeRatesClientTests
{
    private const string AppId = "test-app-id";

    // Shape documented at https://docs.openexchangerates.org/reference/latest-json; timestamp 1761458400 = 2025-10-26T06:00:00Z.
    private const string SuccessBody = """
        {
          "disclaimer": "Usage subject to terms: https://openexchangerates.org/terms",
          "license": "https://openexchangerates.org/license",
          "timestamp": 1761458400,
          "base": "USD",
          "rates": { "EUR": 0.921234, "CAD": 1.401, "GBP": 0.7512, "CHF": 0.7987 }
        }
        """;

    private static readonly Currency[] Targets = [.. new[] { "EUR", "CAD", "GBP", "CHF" }.Select(Currency.Create)];

    [Fact]
    public async Task Request_CallsLatestEndpointWithSymbolsAndWithoutBase()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessBody);

        await GetLatestAsync(handler);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/latest.json", request.Uri.AbsolutePath);
        Assert.Equal("?symbols=EUR,CAD,GBP,CHF", request.Uri.Query);
        // Changing the base is a paid feature; USD is the default, so the parameter is never sent.
        Assert.DoesNotContain("base=", request.Uri.Query);
    }

    [Fact]
    public async Task Request_SendsAppIdInAuthorizationHeaderNotInTheUrl()
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessBody);

        await GetLatestAsync(handler);

        var request = Assert.Single(handler.Requests);
        Assert.Equal($"Token {AppId}", request.Authorization);
        Assert.DoesNotContain(AppId, request.Uri.ToString());
    }

    [Fact]
    public async Task Response_MapsBaseTimestampAndExactDecimalRates()
    {
        var latest = await GetLatestAsync(StubHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessBody));

        Assert.Equal(Currency.USD, latest.Base);
        Assert.Equal(new DateTimeOffset(2025, 10, 26, 6, 0, 0, TimeSpan.Zero), latest.PublishedAt);
        Assert.Equal(4, latest.Rates.Count);
        Assert.Equal(0.921234m, latest.Rates[Currency.Create("EUR")]);
        Assert.Equal(0.7987m, latest.Rates[Currency.Create("CHF")]);
    }

    [Fact]
    public async Task Response_SkipsNonPositiveAndMalformedEntries()
    {
        const string body = """{ "timestamp": 1761458400, "base": "USD", "rates": { "EUR": 0.92, "CHF": 0, "GBP": -1, "XX": 1 } }""";

        var latest = await GetLatestAsync(StubHttpMessageHandler.Returning(HttpStatusCode.OK, body));

        Assert.Equal([Currency.Create("EUR")], latest.Rates.Keys);
    }

    [Fact]
    public async Task Response_WithAnotherBase_Throws()
    {
        const string body = """{ "timestamp": 1761458400, "base": "EUR", "rates": { "USD": 1.08 } }""";

        var exception = await Assert.ThrowsAsync<ExchangeRateProviderException>(
            () => GetLatestAsync(StubHttpMessageHandler.Returning(HttpStatusCode.OK, body)));

        Assert.Contains("EUR", exception.Message);
    }

    [Fact]
    public async Task Response_WithoutRates_Throws()
    {
        const string body = """{ "timestamp": 1761458400, "base": "USD" }""";

        await Assert.ThrowsAsync<ExchangeRateProviderException>(
            () => GetLatestAsync(StubHttpMessageHandler.Returning(HttpStatusCode.OK, body)));
    }

    [Fact]
    public async Task Response_MalformedJson_ThrowsWithTheParserErrorAsInnerException()
    {
        var exception = await Assert.ThrowsAsync<ExchangeRateProviderException>(
            () => GetLatestAsync(StubHttpMessageHandler.Returning(HttpStatusCode.OK, "not json")));

        Assert.IsAssignableFrom<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task ErrorBody_IsTranslatedWithoutLeakingTheAppId()
    {
        // Error shape documented at https://docs.openexchangerates.org/reference/errors.
        const string body = """
            { "error": true, "status": 401, "message": "invalid_app_id", "description": "Invalid App ID provided. Please sign up at https://openexchangerates.org/signup." }
            """;

        var exception = await Assert.ThrowsAsync<ExchangeRateProviderException>(
            () => GetLatestAsync(StubHttpMessageHandler.Returning(HttpStatusCode.Unauthorized, body)));

        Assert.Contains("401", exception.Message);
        Assert.Contains("invalid_app_id", exception.Message);
        Assert.DoesNotContain(AppId, exception.Message);
    }

    [Fact]
    public async Task ErrorWithoutOxrBody_StillReportsTheStatus()
    {
        var exception = await Assert.ThrowsAsync<ExchangeRateProviderException>(
            () => GetLatestAsync(StubHttpMessageHandler.Returning(HttpStatusCode.BadGateway, "<html>Bad gateway</html>", "text/html")));

        Assert.Contains("502", exception.Message);
    }

    [Fact]
    public async Task NetworkFailure_IsTranslatedToProviderException()
    {
        var failure = new HttpRequestException("Connection refused");

        var exception = await Assert.ThrowsAsync<ExchangeRateProviderException>(
            () => GetLatestAsync(StubHttpMessageHandler.Throwing(failure)));

        Assert.Same(failure, exception.InnerException);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingAppId_FailsWithoutCallingTheProvider(string? appId)
    {
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessBody);

        var exception = await Assert.ThrowsAsync<ExchangeRateProviderException>(() => GetLatestAsync(handler, appId));

        Assert.Contains("OpenExchangeRates:AppId", exception.Message);
        Assert.Empty(handler.Requests);
    }

    private static Task<LatestRates> GetLatestAsync(StubHttpMessageHandler handler, string? appId = AppId)
    {
        var client = new OpenExchangeRatesClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://oxr.test/api/") },
            Options.Create(new OpenExchangeRatesOptions { AppId = appId }),
            NullLogger<OpenExchangeRatesClient>.Instance);

        return client.GetLatestRatesAsync(Currency.USD, Targets, CancellationToken.None);
    }
}
