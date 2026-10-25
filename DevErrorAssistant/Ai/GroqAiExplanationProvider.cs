using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace DevErrorAssistant.Ai;

/// <summary>
/// AI explanation provider using the official Groq OpenAI-compatible Chat Completions API.
/// </summary>
public class GroqAiExplanationProvider : IAiExplanationProvider
{
    public const string DefaultEndpoint = "https://api.groq.com/openai/v1/chat/completions";
    public const string HttpClientName = "DevErrorAssistant.Groq";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GroqAiExplanationProvider> _logger;

    public string ProviderName => "Groq";

    public GroqAiExplanationProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<GroqAiExplanationProvider> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AiExplanationResult> GenerateExplanationAsync(
        SanitizedExceptionContext context,
        DevErrorAiOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return AiExplanationResult.Unavailable(
                "AI explanation unavailable: Groq API key is not configured. Provide Groq:ApiKey in configuration or User Secrets.");
        }

        var modelName = string.IsNullOrWhiteSpace(options.Model) ? "llama-3.3-70b-versatile" : options.Model;

        var requestPayload = BuildChatPayload(context, modelName);
        var jsonContent = JsonSerializer.Serialize(requestPayload);

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, DefaultEndpoint)
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };

        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey.Trim());

        using var timeoutCts = new CancellationTokenSource(options.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await httpClient.SendAsync(requestMessage, linkedCts.Token);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Groq API returned HTTP 401 Unauthorized. The configured API key is invalid or lacks access.");
                return AiExplanationResult.Unavailable("AI explanation unavailable: Invalid or unauthorized Groq API key.");
            }

            if (response.StatusCode == (HttpStatusCode)429)
            {
                _logger.LogWarning("Groq API returned HTTP 429 Too Many Requests (rate limited).");
                return AiExplanationResult.Unavailable("AI explanation unavailable: Groq API rate limit reached. Please retry later.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(linkedCts.Token);
                string? specificMessage = null;

                try
                {
                    using var errDoc = JsonDocument.Parse(errorBody);
                    if (errDoc.RootElement.TryGetProperty("error", out var errElement) &&
                        errElement.TryGetProperty("message", out var msgElement))
                    {
                        specificMessage = msgElement.GetString();
                    }
                }
                catch
                {
                    // Fall back to HTTP status code if JSON parsing fails
                }

                _logger.LogWarning("Groq API request failed with status code {StatusCode}: {ErrorDetails}",
                    (int)response.StatusCode, specificMessage ?? errorBody);

                var userNotice = !string.IsNullOrWhiteSpace(specificMessage)
                    ? $"AI explanation unavailable: {specificMessage} (HTTP {(int)response.StatusCode})."
                    : $"AI explanation unavailable: Groq service returned HTTP {(int)response.StatusCode}.";

                return AiExplanationResult.Unavailable(userNotice);
            }

            var responseBody = await response.Content.ReadAsStringAsync(linkedCts.Token);
            return ParseGroqResponse(responseBody, modelName);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Request to Groq API timed out after {TimeoutSeconds} seconds.", options.Timeout.TotalSeconds);
            return AiExplanationResult.Unavailable("AI explanation unavailable: Request to Groq timed out.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Request to Groq API was cancelled.");
            return AiExplanationResult.Unavailable("AI explanation unavailable: Request was cancelled.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP network error while attempting to reach Groq API endpoint.");
            return AiExplanationResult.Unavailable("AI explanation unavailable: Network connection to Groq API failed.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON response from Groq API.");
            return AiExplanationResult.Unavailable("AI explanation unavailable: Received invalid response format from Groq.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred during Groq AI explanation generation.");
            return AiExplanationResult.Unavailable("AI explanation unavailable: An unexpected error occurred.");
        }
    }

    private static object BuildChatPayload(SanitizedExceptionContext context, string modelName)
    {
        const string systemPrompt =
            "You are a senior .NET developer assistant. Analyze the unhandled runtime exception context. " +
            "Provide helpful, practical diagnostic guidance for a developer. " +
            "Structure your response with these exact markdown sections:\n" +
            "### Explanation\n" +
            "(Plain English description of what caused the exception)\n" +
            "### Likely Causes\n" +
            "(2-4 bullet points of probable root causes)\n" +
            "### Recommended Steps\n" +
            "(Step-by-step investigation guidance)\n" +
            "### Possible Fixes\n" +
            "(Concrete code or configuration solutions)\n" +
            "Be concise, technical, and accurate. Do not output raw HTML tags.";

        var userBuilder = new StringBuilder();
        userBuilder.AppendLine($"Exception Type: {context.ExceptionType}");
        userBuilder.AppendLine($"Message: {context.SanitizedMessage}");

        if (!string.IsNullOrEmpty(context.InnerExceptionType))
        {
            userBuilder.AppendLine($"Inner Exception: {context.InnerExceptionType}: {context.SanitizedInnerMessage}");
        }

        if (!string.IsNullOrEmpty(context.SanitizedStackTrace))
        {
            userBuilder.AppendLine("Stack Trace:");
            userBuilder.AppendLine(context.SanitizedStackTrace);
        }

        return new
        {
            model = modelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userBuilder.ToString() }
            },
            temperature = 0.2,
            max_tokens = 1024
        };
    }

    private static AiExplanationResult ParseGroqResponse(string responseJson, string modelName)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return AiExplanationResult.Unavailable("AI explanation unavailable: Groq returned no completion choices.");
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var contentElement))
        {
            return AiExplanationResult.Unavailable("AI explanation unavailable: Incomplete message in Groq response.");
        }

        var fullContent = contentElement.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fullContent))
        {
            return AiExplanationResult.Unavailable("AI explanation unavailable: Empty response from Groq.");
        }

        // Parse sections based on markdown headers
        var (summary, likelyCauses, steps, fixes) = ExtractSections(fullContent);

        return AiExplanationResult.Success(
            summary: summary,
            likelyCauses: likelyCauses,
            investigationSteps: steps,
            possibleFixes: fixes,
            providerModel: $"Groq ({modelName})");
    }

    private static (string Summary, string LikelyCauses, string Steps, string Fixes) ExtractSections(string content)
    {
        string summary = string.Empty;
        string likelyCauses = string.Empty;
        string steps = string.Empty;
        string fixes = string.Empty;

        var sections = content.Split(new[] { "### " }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var section in sections)
        {
            var trimmed = section.Trim();
            if (trimmed.StartsWith("Explanation", StringComparison.OrdinalIgnoreCase))
            {
                summary = StripHeader(trimmed, "Explanation");
            }
            else if (trimmed.StartsWith("Likely Causes", StringComparison.OrdinalIgnoreCase))
            {
                likelyCauses = StripHeader(trimmed, "Likely Causes");
            }
            else if (trimmed.StartsWith("Recommended Steps", StringComparison.OrdinalIgnoreCase))
            {
                steps = StripHeader(trimmed, "Recommended Steps");
            }
            else if (trimmed.StartsWith("Possible Fixes", StringComparison.OrdinalIgnoreCase))
            {
                fixes = StripHeader(trimmed, "Possible Fixes");
            }
        }

        // Fallback if headers were not parsed strictly
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = content;
        }

        if (string.IsNullOrWhiteSpace(fixes))
        {
            fixes = "Inspect the stack trace and application logs to locate the failing code path.";
        }

        return (summary, likelyCauses, steps, fixes);
    }

    private static string StripHeader(string section, string header)
    {
        if (section.StartsWith(header, StringComparison.OrdinalIgnoreCase))
        {
            return section[header.Length..].TrimStart(':', '\r', '\n', ' ');
        }
        return section.Trim();
    }
}
