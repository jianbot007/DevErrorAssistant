namespace DevErrorAssistant.Ai;

/// <summary>
/// Contains sanitized exception details stripped of obvious secrets and bounded in size,
/// safe to transmit to an external AI provider.
/// </summary>
public class SanitizedExceptionContext
{
    /// <summary>
    /// The full name of the exception type (e.g. System.InvalidOperationException).
    /// </summary>
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>
    /// The sanitized and length-bounded exception message.
    /// </summary>
    public string SanitizedMessage { get; set; } = string.Empty;

    /// <summary>
    /// The sanitized and length-bounded stack trace.
    /// </summary>
    public string? SanitizedStackTrace { get; set; }

    /// <summary>
    /// The inner exception type if present.
    /// </summary>
    public string? InnerExceptionType { get; set; }

    /// <summary>
    /// The sanitized inner exception message if present.
    /// </summary>
    public string? SanitizedInnerMessage { get; set; }
}
