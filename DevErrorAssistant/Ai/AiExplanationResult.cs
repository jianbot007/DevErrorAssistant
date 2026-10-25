namespace DevErrorAssistant.Ai;

/// <summary>
/// Represents the result of an AI exception explanation attempt.
/// </summary>
public class AiExplanationResult
{
    /// <summary>
    /// Gets or sets whether the AI provider successfully produced an explanation.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets a human-readable status description (e.g. "Completed", "AI explanation disabled", "Rate limit reached").
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the plain-English summary of what the exception means.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Gets or sets bullet points or descriptions of likely root causes.
    /// </summary>
    public string? LikelyCauses { get; set; }

    /// <summary>
    /// Gets or sets recommended troubleshooting and investigation steps.
    /// </summary>
    public string? InvestigationSteps { get; set; }

    /// <summary>
    /// Gets or sets suggested code fixes or resolutions.
    /// </summary>
    public string? PossibleFixes { get; set; }

    /// <summary>
    /// Gets or sets the name of the model and provider used.
    /// </summary>
    public string? ProviderModel { get; set; }

    /// <summary>
    /// Explicit disclaimer that AI suggestions are not guaranteed fixes.
    /// </summary>
    public string Disclaimer { get; set; } = "AI-generated suggestions are diagnostic guidance and may be incomplete or incorrect. Review all recommendations carefully before applying fixes.";

    /// <summary>
    /// Creates a successful explanation result.
    /// </summary>
    public static AiExplanationResult Success(
        string summary,
        string likelyCauses,
        string investigationSteps,
        string possibleFixes,
        string providerModel)
    {
        return new AiExplanationResult
        {
            IsSuccess = true,
            Status = "Completed",
            Summary = summary,
            LikelyCauses = likelyCauses,
            InvestigationSteps = investigationSteps,
            PossibleFixes = possibleFixes,
            ProviderModel = providerModel
        };
    }

    /// <summary>
    /// Creates an unavailable or failure result with a safe status message.
    /// </summary>
    public static AiExplanationResult Unavailable(string statusMessage)
    {
        return new AiExplanationResult
        {
            IsSuccess = false,
            Status = statusMessage,
            Summary = statusMessage,
            PossibleFixes = "Review application logs and the exception stack trace to diagnose the issue."
        };
    }
}
