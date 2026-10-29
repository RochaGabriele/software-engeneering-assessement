using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Demo.IntegrationTests.OpenExchangeRates;

/// <summary>Answers every request with a canned response and records what was sent.</summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<CancellationToken, Task<HttpResponseMessage>> _respond;

    private StubHttpMessageHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

    public List<RecordedRequest> Requests { get; } = [];

    public static StubHttpMessageHandler Returning(HttpStatusCode statusCode, string body, string contentType = "application/json") =>
        new(_ =>
        {
            var content = new StringContent(body, Encoding.UTF8);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = content });
        });

    public static StubHttpMessageHandler Throwing(Exception exception) => new(_ => throw exception);

    /// <summary>Never answers on its own: the request ends only when it is cancelled (e.g. by HttpClient.Timeout).</summary>
    public static StubHttpMessageHandler Hanging() =>
        new(async cancellationToken =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new UnreachableException();
        });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString()));
        return _respond(cancellationToken);
    }

    internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization);
}
