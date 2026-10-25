namespace DevErrorAssistant.Ai;

/// <summary>
/// Service coordinating exception sanitization, AI provider dispatch, and graceful degradation.
/// </summary>
public interface IExceptionExplanationService
{
    /// <summary>
    /// Generates an AI-powered explanation and troubleshooting suggestions for the given exception.
    /// </summary>
    /// <param name="exception">The unhandled exception.</param>
    /// <param name="options">DevErrorAssistant options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A typed AI explanation result.</returns>
    Task<AiExplanationResult> ExplainAsync(
        Exception exception,
        DevErrorAssistantOptions options,
        CancellationToken cancellationToken = default);
}
