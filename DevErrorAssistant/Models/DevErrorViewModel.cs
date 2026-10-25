using DevErrorAssistant.Ai;

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

    #region AI Exception Analysis Fields

    /// <summary>
    /// Plain-English explanation of why the exception may have occurred.
    /// </summary>
    public string Explanation { get; set; } = "Future Implements — AI-generated explanation of this exception.";

    /// <summary>
    /// Bullet points or description of likely root causes.
    /// </summary>
    public string? LikelyCauses { get; set; }

    /// <summary>
    /// Recommended step-by-step troubleshooting instructions.
    /// </summary>
    public string? InvestigationSteps { get; set; }

    /// <summary>
    /// Actionable suggestions or code fixes to resolve the exception.
    /// </summary>
    public string PossibleSolution { get; set; } = "Future Implements — AI-generated suggestions for resolving this exception.";

    /// <summary>
    /// Current status of the AI analysis provider/service.
    /// </summary>
    public string AiStatus { get; set; } = "Not implemented in this MVP.";

    /// <summary>
    /// Indicates whether a successful AI explanation was generated.
    /// </summary>
    public bool IsAiAvailable { get; set; }

    /// <summary>
    /// The provider and model name used for generating the explanation.
    /// </summary>
    public string? AiProviderModel { get; set; }

    /// <summary>
    /// Explicit disclaimer reminding the developer that AI suggestions are diagnostic guidance, not guaranteed fixes.
    /// </summary>
    public string AiDisclaimer { get; set; } = "AI-generated suggestions are diagnostic guidance and may be incomplete or incorrect. Review all recommendations carefully before applying fixes.";

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

    /// <summary>
    /// Applies the result of an AI explanation attempt to this model.
    /// </summary>
    /// <param name="result">The result from <see cref="IExceptionExplanationService"/>.</param>
    public void ApplyAiResult(AiExplanationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        AiStatus = result.Status;
        IsAiAvailable = result.IsSuccess;
        AiProviderModel = result.ProviderModel;

        if (result.IsSuccess)
        {
            if (!string.IsNullOrWhiteSpace(result.Summary))
            {
                Explanation = result.Summary;
            }

            LikelyCauses = result.LikelyCauses;
            InvestigationSteps = result.InvestigationSteps;

            if (!string.IsNullOrWhiteSpace(result.PossibleFixes))
            {
                PossibleSolution = result.PossibleFixes;
            }
        }
        else
        {
            Explanation = result.Summary ?? result.Status;
            if (!string.IsNullOrWhiteSpace(result.PossibleFixes))
            {
                PossibleSolution = result.PossibleFixes;
            }
        }
    }
}
