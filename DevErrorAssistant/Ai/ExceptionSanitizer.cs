
using System.Text.RegularExpressions;

namespace DevErrorAssistant.Ai;

/// <summary>
/// Sanitizes exception context before it is sent to external services.
/// Redacts common credentials, limits input size, and bounds nested exceptions.
/// </summary>
public sealed class ExceptionSanitizer : IExceptionSanitizer
{
    private static readonly TimeSpan RegexTimeout =
        TimeSpan.FromMilliseconds(100);

    private const int MaxInnerExceptionDepth = 5;

    private const string Redacted = "[REDACTED]";

    private const RegexOptions Options =
        RegexOptions.Compiled |
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant;

    private static Regex CreateRegex(string pattern) =>
        new(pattern, Options, RegexTimeout);

    // Authorization headers and cookies.
    private static readonly Regex SensitiveHeaderRegex = CreateRegex(
        @"(?im)^(\s*(?:authorization|proxy-authorization|cookie|set-cookie)\s*:\s*)[^\r\n]*");

    // Connection string credentials. Capture group 1 is the key.
    private static readonly Regex ConnectionStringCredentialsRegex = CreateRegex(
        @"(?i)\b(Password|Pwd|User\s*ID|UID)\s*=\s*[^;,\r\n]*");

    // Common JSON, configuration, and log key/value secrets.
    private static readonly Regex CommonSecretKeyValueRegex = CreateRegex(
        @"(?i)([""']?(?:api[_-]?key|client[_-]?secret|client[_-]?id|auth[_-]?token|access[_-]?token|refresh[_-]?token|id[_-]?token|password|passwd|secret|subscription[_-]?key|signing[_-]?key|private[_-]?key|account[_-]?key)[""']?\s*[:=]\s*)(?:""[^""]*""|'[^']*'|[^,\s;}\]]+)");

    // Query-string secrets, including Azure SAS signatures.
    private static readonly Regex UrlSecretRegex = CreateRegex(
        @"([?&](?:sig|signature|token|access_token|refresh_token|client_secret|api_key|apikey|password|code|key)=)[^&#\s""']*");

    // Bearer and other common authorization token formats.
    private static readonly Regex BearerTokenRegex = CreateRegex(
        @"(?i)\b(Bearer|Basic)\s+[A-Za-z0-9\-._~+/]+=*");

    // JWT-like tokens.
    private static readonly Regex JwtRegex = CreateRegex(
        @"\beyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*");

    // Common provider-specific API keys.
    private static readonly Regex ProviderApiKeyRegex = CreateRegex(
        @"(?i)\b(?:gsk_[A-Za-z0-9_-]{16,}|sk-[A-Za-z0-9_-]{16,}|sk_[A-Za-z0-9_-]{16,}|key_[A-Za-z0-9_-]{16,}|gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,}|AKIA[A-Z0-9]{16})\b");

    // PEM-encoded private keys.
    private static readonly Regex PrivateKeyBlockRegex = CreateRegex(
        @"-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----[\s\S]*?-----END (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----");

    /// <inheritdoc />
    public SanitizedExceptionContext Sanitize(
        Exception exception,
        ExceptionSanitizationOptions options)
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
            stackTrace = Truncate(
                stackTrace,
                options.MaxStackTraceLength);
        }

        string? innerType = null;
        string? innerMessage = null;

        if (exception.InnerException is not null)
        {
            innerType =
                exception.InnerException.GetType().FullName
                ?? exception.InnerException.GetType().Name;

            innerMessage = BuildInnerExceptionSummary(
                exception.InnerException,
                options.MaxMessageLength,
                options.RedactCommonSecrets);
        }

        return new SanitizedExceptionContext
        {
            ExceptionType =
                exception.GetType().FullName
                ?? exception.GetType().Name,

            SanitizedMessage = message,
            SanitizedStackTrace = stackTrace,
            InnerExceptionType = innerType,
            SanitizedInnerMessage = innerMessage
        };
    }

    private static string BuildInnerExceptionSummary(
        Exception innerException,
        int maxLength,
        bool redactSecrets)
    {
        var parts = new List<string>();
        var current = innerException;

        for (var depth = 0;
             current is not null && depth < MaxInnerExceptionDepth;
             depth++, current = current.InnerException)
        {
            var type = current.GetType().FullName
                       ?? current.GetType().Name;

            var message = current.Message ?? string.Empty;

            if (redactSecrets)
            {
                message = RedactSecrets(message);
            }

            parts.Add($"{type}: {message}");
        }

        var summary = string.Join(
            Environment.NewLine,
            parts);

        return Truncate(summary, maxLength);
    }

    private static string RedactSecrets(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var result = input;

        // If a regex times out, fail closed rather than return
        // potentially unsanitized exception content.
        result = SafeReplace(
            SensitiveHeaderRegex,
            result,
            "$1" + Redacted);

        result = SafeReplace(
            PrivateKeyBlockRegex,
            result,
            Redacted);

        result = SafeReplace(
            ConnectionStringCredentialsRegex,
            result,
            "$1=" + Redacted);

        result = SafeReplace(
            CommonSecretKeyValueRegex,
            result,
            "$1" + Redacted);

        result = SafeReplace(
            UrlSecretRegex,
            result,
            "$1" + Redacted);

        result = SafeReplace(
            BearerTokenRegex,
            result,
            "$1 " + Redacted);

        result = SafeReplace(
            JwtRegex,
            result,
            Redacted);

        result = SafeReplace(
            ProviderApiKeyRegex,
            result,
            Redacted);

        return result;
    }

    private static string SafeReplace(
        Regex regex,
        string input,
        string replacement)
    {
        try
        {
            return regex.Replace(input, replacement);
        }
        catch (RegexMatchTimeoutException)
        {
            return "[Content omitted because sanitization timed out]";
        }
    }

    private static string Truncate(
        string? input,
        int maxLength)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        // A non-positive limit must not mean unlimited content.
        if (maxLength <= 0)
        {
            return string.Empty;
        }

        if (input.Length <= maxLength)
        {
            return input;
        }

        const string suffix = "... [truncated]";

        if (maxLength <= suffix.Length)
        {
            return input[..maxLength];
        }

        return string.Concat(
            input.AsSpan(0, maxLength - suffix.Length),
            suffix);
    }
}
