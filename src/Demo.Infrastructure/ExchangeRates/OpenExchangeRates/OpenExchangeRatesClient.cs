using System.Net.Http.Headers;
using System.Text.Json;
using Demo.Application.Modules.ExchangeRates.Ports;
using Demo.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Demo.Infrastructure.ExchangeRates.OpenExchangeRates;

/// <summary>
/// Anti-corruption layer over the Open Exchange Rates "latest" endpoint: translates its wire format and errors
/// into the application's port types (ADR-0007). It translates, it does not decide: whether the base is acceptable
/// is the sync use case's rule. Transient failures are retried by the resilience pipeline on the typed HttpClient.
/// </summary>
internal sealed class OpenExchangeRatesClient : IExchangeRateProvider
{
    private static readonly long MaxUnixSeconds = DateTimeOffset.MaxValue.ToUnixTimeSeconds();

    private readonly HttpClient _httpClient;
    private readonly IOptions<OpenExchangeRatesOptions> _options;
    private readonly ILogger<OpenExchangeRatesClient> _logger;

    public OpenExchangeRatesClient(HttpClient httpClient, IOptions<OpenExchangeRatesOptions> options, ILogger<OpenExchangeRatesClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<LatestRates> GetLatestRatesAsync(Currency baseCurrency, IReadOnlyCollection<Currency> targets, CancellationToken cancellationToken)
    {
        var appId = _options.Value.AppId;
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ExchangeRateProviderException(
                "The Open Exchange Rates App ID is not configured. Set 'OpenExchangeRates:AppId' with user-secrets " +
                "or the 'OpenExchangeRates__AppId' environment variable.");
        }

        // Codes are validated ISO letters, so the list needs no escaping. 'base' is never sent: changing it is a
        // paid feature and USD is already the default; the response reports the base actually used.
        using var request = new HttpRequestMessage(HttpMethod.Get, $"latest.json?symbols={string.Join(',', targets.Select(target => target.Code))}");

        // Header rather than the documented app_id query parameter, so the secret never appears in URLs or request logs.
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", appId);

        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateProviderExceptionAsync(response, cancellationToken);
        }

        var payload = await ReadLatestRatesAsync(response, cancellationToken);

        if (!Currency.TryParse(payload.Base, out var responseBase))
        {
            throw new ExchangeRateProviderException($"Open Exchange Rates returned a response without a valid base currency ('{payload.Base}').");
        }

        // A missing timestamp must not become 1970-01-01: every stored rate would then look newer than the response.
        if (payload.Timestamp is not { } seconds || seconds <= 0 || seconds > MaxUnixSeconds)
        {
            throw new ExchangeRateProviderException($"Open Exchange Rates returned a response without a valid timestamp ('{payload.Timestamp}').");
        }

        var rates = new Dictionary<Currency, decimal>();
        foreach (var (code, value) in payload.Rates!)
        {
            if (Currency.TryParse(code, out var currency) && value is > 0)
            {
                rates[currency] = value.Value;
            }
            else
            {
                _logger.LogWarning("Ignoring unusable Open Exchange Rates entry {Code} = {Value}.", code, value);
            }
        }

        return new LatestRates(responseBase, DateTimeOffset.FromUnixTimeSeconds(seconds), rates);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (IsProviderFailure(exception, cancellationToken))
        {
            throw new ExchangeRateProviderException($"Open Exchange Rates could not be reached: {exception.Message}", exception);
        }
    }

    // Network errors, the resilience pipeline giving up, or HttpClient.Timeout elapsing (which also covers buffering the
    // body, outside the pipeline). A cancellation requested by the caller (e.g. Hangfire shutting down) is not a failure.
    private static bool IsProviderFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or TimeoutRejectedException or BrokenCircuitException
        || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static async Task<LatestRatesResponse> ReadLatestRatesAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        LatestRatesResponse? payload;
        try
        {
            payload = await ReadJsonAsync<LatestRatesResponse>(response, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new ExchangeRateProviderException("Open Exchange Rates returned a malformed response.", exception);
        }

        return payload is { Rates: not null }
            ? payload
            : throw new ExchangeRateProviderException("Open Exchange Rates returned a response without rates.");
    }

    // JSON between systems must be UTF-8 (RFC 8259), so the declared charset is ignored instead of trusted. Only malformed
    // JSON is a provider problem; any other exception here is a bug in this code and must surface as such.
    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(body, JsonSerializerOptions.Web);
    }

    private static async Task<ExchangeRateProviderException> CreateProviderExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;

        ErrorResponse? error = null;
        try
        {
            error = await ReadJsonAsync<ErrorResponse>(response, cancellationToken);
        }
        catch (JsonException)
        {
            // Not an Open Exchange Rates error body (e.g. a proxy's HTML page): report the status alone.
        }

        return error?.Message is { } code
            ? new ExchangeRateProviderException($"Open Exchange Rates request failed with HTTP {status} ({code}): {error.Description}")
            : new ExchangeRateProviderException($"Open Exchange Rates request failed with HTTP {status}.");
    }
}
