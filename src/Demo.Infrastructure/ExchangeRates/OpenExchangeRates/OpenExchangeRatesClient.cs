using System.Net.Http.Headers;
using System.Net.Http.Json;
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
/// into the application's port types (ADR-0007). Transient failures are retried by the resilience pipeline
/// configured on the typed HttpClient.
/// </summary>
internal sealed class OpenExchangeRatesClient : IExchangeRateProvider
{
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
        // paid feature and USD is already the default; the response base is checked instead.
        using var request = new HttpRequestMessage(HttpMethod.Get, $"latest.json?symbols={string.Join(',', targets.Select(target => target.Code))}");

        // Header rather than the documented app_id query parameter, so the secret never appears in URLs or request logs.
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", appId);

        using var response = await SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateProviderExceptionAsync(response, cancellationToken);
        }

        var payload = await ReadLatestRatesAsync(response, cancellationToken);

        if (!Currency.TryParse(payload.Base, out var responseBase) || responseBase != baseCurrency)
        {
            throw new ExchangeRateProviderException(
                $"Open Exchange Rates returned rates based on '{payload.Base}', but {baseCurrency} was requested.");
        }

        var rates = new Dictionary<Currency, decimal>();
        foreach (var (code, value) in payload.Rates!)
        {
            if (Currency.TryParse(code, out var currency) && value > 0)
            {
                rates[currency] = value;
            }
            else
            {
                _logger.LogWarning("Ignoring unusable Open Exchange Rates entry {Code} = {Value}.", code, value);
            }
        }

        return new LatestRates(baseCurrency, DateTimeOffset.FromUnixTimeSeconds(payload.Timestamp), rates);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutRejectedException or BrokenCircuitException)
        {
            throw new ExchangeRateProviderException($"Open Exchange Rates could not be reached: {exception.Message}", exception);
        }
    }

    private static async Task<LatestRatesResponse> ReadLatestRatesAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        LatestRatesResponse? payload;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<LatestRatesResponse>(cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new ExchangeRateProviderException("Open Exchange Rates returned a malformed response.", exception);
        }

        return payload is { Rates: not null }
            ? payload
            : throw new ExchangeRateProviderException("Open Exchange Rates returned a response without rates.");
    }

    private static async Task<ExchangeRateProviderException> CreateProviderExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;

        ErrorResponse? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // Not an Open Exchange Rates error body (e.g. a proxy's HTML page): report the status alone.
        }

        return error?.Message is { } code
            ? new ExchangeRateProviderException($"Open Exchange Rates request failed with HTTP {status} ({code}): {error.Description}")
            : new ExchangeRateProviderException($"Open Exchange Rates request failed with HTTP {status}.");
    }
}
