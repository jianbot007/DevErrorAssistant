namespace DevErrorAssistant.Ai;

/// <summary>
/// Configuration options for optional AI-powered exception explanations.
/// </summary>
public class DevErrorAiOptions
{
    /// <summary>
    /// Gets or sets whether AI-powered exception explanation is enabled.
    /// Default is <c>false</c> (opt-in).
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Gets or sets the AI provider to use.
    /// Default is <c>"Groq"</c>.
    /// </summary>
    public string Provider { get; set; } = "Groq";

    /// <summary>
    /// Gets or sets the API key for the AI provider.
    /// If null or empty, DevErrorAssistant resolves the key at runtime from the host application's
    /// configuration using key <c>"Groq:ApiKey"</c>.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the model name for the AI provider.
    /// Default is <c>"llama-3.3-70b-versatile"</c>.
    /// </summary>
    public string Model { get; set; } = "llama-3.3-70b-versatile";

    /// <summary>
    /// Gets or sets the HTTP request timeout when communicating with the AI provider.
    /// Default is 10 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets options for sanitizing exception context before sending it to an external AI provider.
    /// </summary>
    public ExceptionSanitizationOptions Sanitization { get; set; } = new();
}

/// <summary>
/// Options controlling exception context scrubbing and truncation.
/// </summary>
public class ExceptionSanitizationOptions
{
    /// <summary>
    /// Maximum character length of the exception message sent to the AI provider.
    /// Default is 1000 characters.
    /// </summary>
    public int MaxMessageLength { get; set; } = 1000;

    /// <summary>
    /// Maximum character length of the stack trace sent to the AI provider.
    /// Default is 3000 characters.
    /// </summary>
    public int MaxStackTraceLength { get; set; } = 3000;

    /// <summary>
    /// Whether to redact common credentials, connection strings, bearer tokens, and API keys.
    /// Default is <c>true</c>.
    /// </summary>
    public bool RedactCommonSecrets { get; set; } = true;
}
