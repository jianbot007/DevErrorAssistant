using System.Net;
using DevErrorAssistant.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevErrorAssistant.Tests;

public class GroqAiExplanationProviderTests
{
    private static SanitizedExceptionContext CreateSampleContext() => new()
    {
        ExceptionType = "System.NullReferenceException",
        SanitizedMessage = "Object reference not set to an instance of an object.",
        SanitizedStackTrace = "   at Demo.Services.UserService.Get(Int32 id)"
    };

    [Fact]
    public async Task GenerateExplanationAsync_WhenSuccessful_ParsesSectionsCorrectly()
    {
        var mockResponseBody = @"{
            ""id"": ""chatcmpl-123"",
            ""choices"": [
                {
                    ""message"": {
                        ""role"": ""assistant"",
                        ""content"": ""### Explanation\nA null reference was dereferenced in UserService.Get.\n\n### Likely Causes\n- User entity was null\n- Database returned empty record\n\n### Recommended Steps\n1. Inspect id parameter\n2. Add null check\n\n### Possible Fixes\nUse user?.Name or return NotFound().""
                    }
                }
            ]
        }";

        var fakeHandler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, mockResponseBody);
        var factory = new TestHttpClientFactory(fakeHandler);
        var provider = new GroqAiExplanationProvider(factory, NullLogger<GroqAiExplanationProvider>.Instance);

        var options = new DevErrorAiOptions
        {
            ApiKey = "test-fake-key",
            Model = "llama-3.3-70b-versatile"
        };

        var result = await provider.GenerateExplanationAsync(CreateSampleContext(), options);

        Assert.True(result.IsSuccess);
        Assert.Equal("Completed", result.Status);
        Assert.Contains("A null reference was dereferenced", result.Summary);
        Assert.Contains("User entity was null", result.LikelyCauses);
        Assert.Contains("Inspect id parameter", result.InvestigationSteps);
        Assert.Contains("Use user?.Name", result.PossibleFixes);
        Assert.Contains("Groq (llama-3.3-70b-versatile)", result.ProviderModel);
        Assert.Equal(1, fakeHandler.RequestCount);
    }

    [Fact]
    public async Task GenerateExplanationAsync_WhenApiKeyMissing_ReturnsUnavailableWithoutCallingNetwork()
    {
        var fakeHandler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, "{}");
        var factory = new TestHttpClientFactory(fakeHandler);
        var provider = new GroqAiExplanationProvider(factory, NullLogger<GroqAiExplanationProvider>.Instance);

        var options = new DevErrorAiOptions { ApiKey = null };

        var result = await provider.GenerateExplanationAsync(CreateSampleContext(), options);

        Assert.False(result.IsSuccess);
        Assert.Contains("Groq API key is not configured", result.Status);
        Assert.Equal(0, fakeHandler.RequestCount);
    }

    [Fact]
    public async Task GenerateExplanationAsync_When401Unauthorized_DegradesGracefully()
    {
        var fakeHandler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.Unauthorized, "{\"error\": \"invalid_api_key\"}");
        var factory = new TestHttpClientFactory(fakeHandler);
        var provider = new GroqAiExplanationProvider(factory, NullLogger<GroqAiExplanationProvider>.Instance);

        var options = new DevErrorAiOptions { ApiKey = "invalid-key" };

        var result = await provider.GenerateExplanationAsync(CreateSampleContext(), options);

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid or unauthorized Groq API key", result.Status);
    }

    [Fact]
    public async Task GenerateExplanationAsync_When429RateLimited_DegradesGracefully()
    {
        var fakeHandler = FakeHttpMessageHandler.RespondingWith((HttpStatusCode)429, "{\"error\": \"rate_limit_exceeded\"}");
        var factory = new TestHttpClientFactory(fakeHandler);
        var provider = new GroqAiExplanationProvider(factory, NullLogger<GroqAiExplanationProvider>.Instance);

        var options = new DevErrorAiOptions { ApiKey = "test-key" };

        var result = await provider.GenerateExplanationAsync(CreateSampleContext(), options);

        Assert.False(result.IsSuccess);
        Assert.Contains("rate limit reached", result.Status);
    }

    [Fact]
    public async Task GenerateExplanationAsync_When500ServerError_DegradesGracefully()
    {
        var fakeHandler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.InternalServerError, "Service Unavailable");
        var factory = new TestHttpClientFactory(fakeHandler);
        var provider = new GroqAiExplanationProvider(factory, NullLogger<GroqAiExplanationProvider>.Instance);

        var options = new DevErrorAiOptions { ApiKey = "test-key" };

        var result = await provider.GenerateExplanationAsync(CreateSampleContext(), options);

        Assert.False(result.IsSuccess);
        Assert.Contains("Groq service returned HTTP 500", result.Status);
    }

    [Fact]
    public async Task GenerateExplanationAsync_WhenJsonMalformed_DegradesGracefully()
    {
        var fakeHandler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, "{ invalid json text }");
        var factory = new TestHttpClientFactory(fakeHandler);
        var provider = new GroqAiExplanationProvider(factory, NullLogger<GroqAiExplanationProvider>.Instance);

        var options = new DevErrorAiOptions { ApiKey = "test-key" };

        var result = await provider.GenerateExplanationAsync(CreateSampleContext(), options);

        Assert.False(result.IsSuccess);
        Assert.Contains("invalid response format", result.Status);
    }

    [Fact]
    public async Task GenerateExplanationAsync_WhenNetworkThrows_DegradesGracefully()
    {
        var fakeHandler = FakeHttpMessageHandler.Throwing(new HttpRequestException("Connection refused"));
        var factory = new TestHttpClientFactory(fakeHandler);
        var provider = new GroqAiExplanationProvider(factory, NullLogger<GroqAiExplanationProvider>.Instance);

        var options = new DevErrorAiOptions { ApiKey = "test-key" };

        var result = await provider.GenerateExplanationAsync(CreateSampleContext(), options);

        Assert.False(result.IsSuccess);
        Assert.Contains("Network connection to Groq API failed", result.Status);
    }

    [Fact]
    public async Task GenerateExplanationAsync_WhenRequestCancelled_HandlesCancellationCorrectly()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        var fakeHandler = FakeHttpMessageHandler.RespondingWith(HttpStatusCode.OK, "{}");
        var factory = new TestHttpClientFactory(fakeHandler);
        var provider = new GroqAiExplanationProvider(factory, NullLogger<GroqAiExplanationProvider>.Instance);

        var options = new DevErrorAiOptions { ApiKey = "test-key" };

        var result = await provider.GenerateExplanationAsync(CreateSampleContext(), options, cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Contains("cancelled", result.Status);
    }
}
