using System.Net;
using System.Text;

namespace Demo.IntegrationTests.OpenExchangeRates;

/// <summary>Answers every request with a canned response and records what was sent.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpResponseMessage> _respond;

    private StubHttpMessageHandler(Func<HttpResponseMessage> respond) => _respond = respond;

    public List<RecordedRequest> Requests { get; } = [];

    public static StubHttpMessageHandler Returning(HttpStatusCode statusCode, string body, string mediaType = "application/json") =>
        new(() => new HttpResponseMessage(statusCode) { Content = new StringContent(body, Encoding.UTF8, mediaType) });

    public static StubHttpMessageHandler Throwing(Exception exception) => new(() => throw exception);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString()));
        return Task.FromResult(_respond());
    }

    internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization);
}
