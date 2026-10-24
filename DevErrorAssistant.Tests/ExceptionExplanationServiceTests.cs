using DevErrorAssistant.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DevErrorAssistant.Tests;

public class ExceptionExplanationServiceTests
{
    private class MockProvider : IAiExplanationProvider
    {
        public string ProviderName { get; }
        public int CallCount { get; private set; }
        public SanitizedExceptionContext? LastContext { get; private set; }
        public DevErrorAiOptions? LastOptions { get; private set; }

        public MockProvider(string name = "Groq")
        {
            ProviderName = name;
        }

        public Task<AiExplanationResult> GenerateExplanationAsync(
            SanitizedExceptionContext context,
            DevErrorAiOptions options,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastContext = context;
            LastOptions = options;
            return Task.FromResult(AiExplanationResult.Success(
                summary: "Mock explanation",
                likelyCauses: "Mock cause",
                investigationSteps: "Mock step",
                possibleFixes: "Mock fix",
                providerModel: $"{ProviderName} (mock)"));
        }
    }

    [Fact]
    public async Task ExplainAsync_WhenAiDisabled_ReturnsUnavailableWithoutCallingProvider()
    {
        var mockProvider = new MockProvider();
        var service = new ExceptionExplanationService(
            new[] { mockProvider },
            new ExceptionSanitizer(),
            NullLogger<ExceptionExplanationService>.Instance);

        var options = new DevErrorAssistantOptions();
        options.Ai.Enabled = false;

        var result = await service.ExplainAsync(new Exception("test"), options);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, mockProvider.CallCount);
        Assert.Contains("disabled", result.Status);
    }

    [Fact]
    public async Task ExplainAsync_WhenApiKeyMissingFromConfigAndOptions_ReturnsUnavailable()
    {
        var mockProvider = new MockProvider();
        var emptyConfig = new ConfigurationBuilder().Build();

        var service = new ExceptionExplanationService(
            new[] { mockProvider },
            new ExceptionSanitizer(),
            NullLogger<ExceptionExplanationService>.Instance,
            emptyConfig);

        var options = new DevErrorAssistantOptions();
        options.Ai.Enabled = true;
        options.Ai.ApiKey = null;

        var result = await service.ExplainAsync(new Exception("test"), options);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, mockProvider.CallCount);
        Assert.Contains("API key is not configured", result.Status);
    }

    [Fact]
    public async Task ExplainAsync_WhenApiKeyInConfiguration_ResolvesKeyAndInvokesProvider()
    {
        var mockProvider = new MockProvider();
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Groq:ApiKey", "config-resolved-key-12345" }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();

        var service = new ExceptionExplanationService(
            new[] { mockProvider },
            new ExceptionSanitizer(),
            NullLogger<ExceptionExplanationService>.Instance,
            config);

        var options = new DevErrorAssistantOptions();
        options.Ai.Enabled = true;
        options.Ai.ApiKey = null; // empty in options, should resolve from config

        var result = await service.ExplainAsync(new InvalidOperationException("Test exception"), options);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, mockProvider.CallCount);
        Assert.Equal("config-resolved-key-12345", mockProvider.LastOptions?.ApiKey);
        Assert.Equal("Mock explanation", result.Summary);
    }
}
