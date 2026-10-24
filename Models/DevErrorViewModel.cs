namespace DevErrorAssistant.Models;

/// <summary>
/// Represents the diagnostic and context data rendered on the DevErrorAssistant error page.
/// </summary>
public class DevErrorViewModel
{
    /// <summary>
    /// Gets or sets the exception type name (e.g., System.InvalidOperationException).
    /// In production or when diagnostics are hidden, this displays a safe generic title.
    /// </summary>
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exception message.
    /// In production or when diagnostics are hidden, this displays a safe generic message.
    /// </summary>
    public string ExceptionMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stack trace of the exception.
    /// This is null or omitted when diagnostic details are hidden.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Gets or sets whether detailed diagnostics (raw exception type, message, stack trace) are visible.
    /// </summary>
    public bool ShowDetails { get; set; }

    #region Future Implements: AI Exception Analysis Extension Points

    // Future Implements:
    // In future versions, an AI analysis service (e.g. OpenAI, Claude, Gemini, or local Ollama)
    // will populate these fields with contextual troubleshooting guidance.
    // In this MVP, static placeholders are displayed and no AI services are contacted.

    /// <summary>
    /// Plain-English explanation of why the exception may have occurred.
    /// </summary>
    public string Explanation { get; set; } = "Future Implements — AI-generated explanation of this exception.";

    /// <summary>
    /// Actionable suggestions or code fixes to resolve the exception.
    /// </summary>
    public string PossibleSolution { get; set; } = "Future Implements — AI-generated suggestions for resolving this exception.";

    /// <summary>
    /// Current status of the AI analysis provider/service.
    /// </summary>
    public string AiStatus { get; set; } = "Not implemented in this MVP.";

    #endregion

    /// <summary>
    /// Creates a <see cref="DevErrorViewModel"/> populated from an exception, respecting diagnostic privacy.
    /// </summary>
    /// <param name="exception">The unhandled exception caught by middleware.</param>
    /// <param name="showDetails">Whether detailed diagnostics should be shown.</param>
    /// <returns>A populated <see cref="DevErrorViewModel"/>.</returns>
    public static DevErrorViewModel FromException(Exception exception, bool showDetails)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (showDetails)
        {
            return new DevErrorViewModel
            {
                ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                ExceptionMessage = exception.Message,
                StackTrace = exception.StackTrace,
                ShowDetails = true,
                Explanation = "Future Implements — AI-generated explanation of this exception.",
                PossibleSolution = "Future Implements — AI-generated suggestions for resolving this exception.",
                AiStatus = "Not implemented in this MVP."
            };
        }

        return new DevErrorViewModel
        {
            ExceptionType = "An error occurred",
            ExceptionMessage = "An unexpected error occurred while processing your request. Detailed diagnostic information is disabled in this environment.",
            StackTrace = null,
            ShowDetails = false,
            Explanation = "Future Implements — AI-generated explanation of this exception.",
            PossibleSolution = "Future Implements — AI-generated suggestions for resolving this exception.",
            AiStatus = "Not implemented in this MVP."
        };
    }
}
