using System.Text.RegularExpressions;

namespace DevErrorAssistant.Ai;

/// <summary>
/// Sanitizes and truncates exception context to mitigate credential leakage before external transmission.
/// </summary>
public class ExceptionSanitizer : IExceptionSanitizer
{
    private static readonly Regex BearerTokenRegex = new(
        @"(?i)bearer\s+[a-z0-9\-._~+/]+=*",
        RegexOptions.Compiled);

    private static readonly Regex ConnectionStringCredentialsRegex = new(
        @"(?i)(?:password|pwd|user\s*id|uid)\s*=\s*[^;]+",
        RegexOptions.Compiled);

    private static readonly Regex CommonSecretKeyValueRegex = new(
        @"(?i)(api[_-]?key|client[_-]?secret|auth[_-]?token|access[_-]?token|password|secret)\s*[:=]\s*[""']?[^\s""',;]{4,}[""']?",
        RegexOptions.Compiled);

    private static readonly Regex SpecificApiKeyPrefixRegex = new(
        @"(?i)(?:gsk|sk|key)_[a-z0-9]{16,}",
        RegexOptions.Compiled);

    /// <inheritdoc />
    public SanitizedExceptionContext Sanitize(Exception exception, ExceptionSanitizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(options);

        var message = exception.Message ?? string.Empty;
        var stackTrace = exception.StackTrace;

        if (options.RedactCommonSecrets)
        {
            message = RedactSecrets(message);
            if (!string.IsNullOrEmpty(stackTrace))
            {
                stackTrace = RedactSecrets(stackTrace);
            }
        }

        message = Truncate(message, options.MaxMessageLength);
        if (!string.IsNullOrEmpty(stackTrace))
        {
            stackTrace = Truncate(stackTrace, options.MaxStackTraceLength);
        }

        string? innerType = null;
        string? innerMessage = null;

        if (exception.InnerException != null)
        {
            innerType = exception.InnerException.GetType().FullName ?? exception.InnerException.GetType().Name;
            innerMessage = exception.InnerException.Message ?? string.Empty;

            if (options.RedactCommonSecrets)
            {
                innerMessage = RedactSecrets(innerMessage);
            }

            innerMessage = Truncate(innerMessage, options.MaxMessageLength);
        }

        return new SanitizedExceptionContext
        {
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            SanitizedMessage = message,
            SanitizedStackTrace = stackTrace,
            InnerExceptionType = innerType,
            SanitizedInnerMessage = innerMessage
        };
    }

    private static string RedactSecrets(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        // 1. Redact Bearer tokens
        var result = BearerTokenRegex.Replace(input, "Bearer [REDACTED_TOKEN]");

        // 2. Redact connection string credentials (e.g. Password=secret; User ID=admin)
        result = ConnectionStringCredentialsRegex.Replace(result, "$1=[REDACTED]");

        // 3. Redact common key-value secret pairs (e.g. ApiKey: abc12345, "password": "xyz")
        result = CommonSecretKeyValueRegex.Replace(result, "$1: [REDACTED]");

        // 4. Redact specific API key patterns (e.g. gsk_*, sk_*)
        result = SpecificApiKeyPrefixRegex.Replace(result, "[REDACTED_KEY]");

        return result;
    }

    private static string Truncate(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input) || maxLength <= 0 || input.Length <= maxLength)
        {
            return input;
        }

        const string suffix = "... [truncated]";
        if (maxLength <= suffix.Length)
        {
            return input[..maxLength];
        }

        return string.Concat(input.AsSpan(0, maxLength - suffix.Length), suffix);
    }
}
