using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevErrorAssistant.Ai;

/// <summary>
/// Default implementation of <see cref="IExceptionExplanationService"/> coordinating sanitization,
/// API key resolution, provider dispatch, and fault-tolerant fallback.
/// </summary>
public class ExceptionExplanationService : IExceptionExplanationService
{
    private readonly IEnumerable<IAiExplanationProvider> _providers;
    private readonly IExceptionSanitizer _sanitizer;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<ExceptionExplanationService> _logger;

    public ExceptionExplanationService(
        IEnumerable<IAiExplanationProvider> providers,
        IExceptionSanitizer sanitizer,
        ILogger<ExceptionExplanationService> logger,
        IConfiguration? configuration = null)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration;
    }

    /// <inheritdoc />
    public async Task<AiExplanationResult> ExplainAsync(
        Exception exception,
        DevErrorAssistantOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(options);

        // 1. Opt-in check: If AI is disabled, immediately return without making network calls
        if (!options.Ai.Enabled)
        {
            return AiExplanationResult.Unavailable("AI explanation disabled in configuration.");
        }

        try
        {
            // 2. Resolve API key from options or host configuration
            var apiKey = options.Ai.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey) && _configuration != null)
            {
                apiKey = _configuration[$"{options.Ai.Provider}:ApiKey"] ?? _configuration["Groq:ApiKey"];
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogInformation("AI explanation is enabled, but no API key was found for provider '{Provider}'.", options.Ai.Provider);
                return AiExplanationResult.Unavailable(
                    $"AI explanation unavailable: {options.Ai.Provider} API key is not configured. Provide {options.Ai.Provider}:ApiKey in configuration or User Secrets.");
            }

            // 3. Find matching provider
            var provider = _providers.FirstOrDefault(p =>
                string.Equals(p.ProviderName, options.Ai.Provider, StringComparison.OrdinalIgnoreCase));

            if (provider == null)
            {
                _logger.LogWarning("Configured AI provider '{Provider}' was not found among registered providers.", options.Ai.Provider);
                return AiExplanationResult.Unavailable($"AI explanation unavailable: Provider '{options.Ai.Provider}' is not supported.");
            }

            // 4. Sanitize exception context
            var sanitizedContext = _sanitizer.Sanitize(exception, options.Ai.Sanitization);

            // 5. Clone options with resolved API key to avoid mutating external state
            var effectiveAiOptions = new DevErrorAiOptions
            {
                Enabled = true,
                Provider = options.Ai.Provider,
                ApiKey = apiKey,
                Model = options.Ai.Model,
                Timeout = options.Ai.Timeout,
                Sanitization = options.Ai.Sanitization
            };

            // 6. Invoke provider
            return await provider.GenerateExplanationAsync(sanitizedContext, effectiveAiOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure in exception explanation service.");
            return AiExplanationResult.Unavailable("AI explanation unavailable: An unexpected error occurred.");
        }
    }
}
