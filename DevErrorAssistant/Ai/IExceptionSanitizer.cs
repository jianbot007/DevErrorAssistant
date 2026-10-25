namespace DevErrorAssistant.Ai;

/// <summary>
/// Provides methods to sanitize and truncate exception context before transmission to external services.
/// </summary>
public interface IExceptionSanitizer
{
    /// <summary>
    /// Sanitizes an exception by stripping known secret patterns and bounding text lengths.
    /// </summary>
    /// <param name="exception">The unhandled exception.</param>
    /// <param name="options">Sanitization options.</param>
    /// <returns>A sanitized exception context DTO.</returns>
    SanitizedExceptionContext Sanitize(Exception exception, ExceptionSanitizationOptions options);
}
