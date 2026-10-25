namespace DevErrorAssistant.Ai;

/// <summary>
/// Defines an extensible provider contract for generating AI-powered exception explanations.
/// </summary>
public interface IAiExplanationProvider
{
    /// <summary>
    /// Gets the unique name of this provider (e.g. "Groq").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Generates an explanation for the given sanitized exception context.
    /// </summary>
    /// <param name="context">The sanitized exception context.</param>
    /// <param name="options">The AI options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A typed AI explanation result.</returns>
    Task<AiExplanationResult> GenerateExplanationAsync(
        SanitizedExceptionContext context,
        DevErrorAiOptions options,
        CancellationToken cancellationToken = default);
}
