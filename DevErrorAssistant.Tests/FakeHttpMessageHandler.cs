using System.Net;

namespace DevErrorAssistant.Tests;

/// <summary>
/// Deterministic test double for HttpMessageHandler that intercepts requests without making real network calls.
/// </summary>
public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public int RequestCount { get; private set; }
    public HttpRequestMessage? LastRequest { get; private set; }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public static FakeHttpMessageHandler RespondingWith(
        HttpStatusCode statusCode,
        string content,
        string mediaType = "application/json")
    {
        return new FakeHttpMessageHandler((req, ct) =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, mediaType)
            };
            return Task.FromResult(response);
        });
    }

    public static FakeHttpMessageHandler Throwing(Exception exception)
    {
        return new FakeHttpMessageHandler((req, ct) => Task.FromException<HttpResponseMessage>(exception));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequest = request;
        return await _handler(request, cancellationToken);
    }
}

public class TestHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public TestHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public HttpClient CreateClient(string name)
    {
        return new HttpClient(_handler, disposeHandler: false);
    }
}
