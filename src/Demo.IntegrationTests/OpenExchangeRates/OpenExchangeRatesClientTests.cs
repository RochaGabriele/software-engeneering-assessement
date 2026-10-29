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
    public async Task Response_ReportsTheBaseTheProviderActuallyUsed()
    {
        // The adapter only translates; the sync use case decides whether a different base is acceptable.
        const string body = """{ "timestamp": 1761458400, "base": "EUR", "rates": { "USD": 1.08 } }""";

        var latest = await GetLatestAsync(StubHttpMessageHandler.Returning(HttpStatusCode.OK, body));

        Assert.Equal(Currency.Create("EUR"), latest.Base);
    }

    [Theory]
    [InlineData("""{ "timestamp": 1761458400, "rates": { "EUR": 0.92 } }""")]
    [InlineData("""{ "timestamp": 1761458400, "base": "DOLLARS", "rates": { "EUR": 0.92 } }""")]
    public async Task Response_WithoutAValidBase_Throws(string body)
    {
        var exception = await Assert.ThrowsAsync<ExchangeRateProviderException>(
            () => GetLatestAsync(StubHttpMessageHandler.Returning(HttpStatusCode.OK, body)));

        Assert.Contains("base", exception.Message);
    }

    [Theory]
    [InlineData("""{ "base": "USD", "rates": { "EUR": 0.92 } }""")]
    [InlineData("""{ "timestamp": 0, "base": "USD", "rates": { "EUR": 0.92 } }""")]
    [InlineData("""{ "timestamp": 99999999999999, "base": "USD", "rates": { "EUR": 0.92 } }""")]
    public async Task Response_WithoutAValidTimestamp_Throws(string body)
    {
        // A missing timestamp would otherwise become 1970-01-01 and every stored rate would look "newer".
        var exception = await Assert.ThrowsAsync<ExchangeRateProviderException>(
            () => GetLatestAsync(StubHttpMessageHandler.Returning(HttpStatusCode.OK, body)));

        Assert.Contains("timestamp", exception.Message);
    }

    [Fact]
    public async Task Response_NullRateEntry_IsSkippedLikeOtherUnusableEntries()
    {
        const string body = """{ "timestamp": 1761458400, "base": "USD", "rates": { "EUR": 0.92, "GBP": null } }""";

        var latest = await GetLatestAsync(StubHttpMessageHandler.Returning(HttpStatusCode.OK, body));

        Assert.Equal([Currency.Create("EUR")], latest.Rates.Keys);
    }

    [Fact]
    public async Task Response_DeclaringAnUnknownCharset_IsStillReadAsUtf8Json()
    {
        // RFC 8259 requires JSON exchanged between systems to be UTF-8, so a bogus charset label must not break parsing.
        var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessBody, "application/json; charset=x-unknown");

        var latest = await GetLatestAsync(handler);

        Assert.Equal(0.921234m, latest.Rates[Currency.Create("EUR")]);
    }

    [Fact]
    public async Task HttpClientTimeout_IsTranslatedToProviderException()
    {
        var exception = await Assert.ThrowsAsync<ExchangeRateProviderException>(
            () => GetLatestAsync(StubHttpMessageHandler.Hanging(), timeout: TimeSpan.FromMilliseconds(100)));

        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
    }

    [Fact]
    public async Task CallerCancellation_IsNotDisguisedAsAProviderFailure()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => GetLatestAsync(StubHttpMessageHandler.Hanging(), cancellationToken: cancellation.Token));

        Assert.IsNotType<ExchangeRateProviderException>(exception);
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

    private static Task<LatestRates> GetLatestAsync(
        StubHttpMessageHandler handler,
        string? appId = AppId,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://oxr.test/api/") };
        if (timeout is { } value)
        {
            httpClient.Timeout = value;
        }

        var client = new OpenExchangeRatesClient(
            httpClient,
            Options.Create(new OpenExchangeRatesOptions { AppId = appId }),
            NullLogger<OpenExchangeRatesClient>.Instance);

        return client.GetLatestRatesAsync(Currency.USD, Targets, cancellationToken);
    }
}
