
using DevErrorAssistant.Ai;
using Xunit;

namespace DevErrorAssistant.Tests;

public class ExceptionSanitizerTests
{
    private readonly ExceptionSanitizer _sanitizer = new();

    private static ExceptionSanitizationOptions DefaultOptions() =>
        new()
        {
            RedactCommonSecrets = true
        };

    [Fact]
    public void Sanitize_RedactsBearerTokens()
    {
        var ex = new InvalidOperationException(
            "Failed auth call: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e30.signature");

        var result = _sanitizer.Sanitize(ex, DefaultOptions());

        Assert.DoesNotContain(
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9",
            result.SanitizedMessage);

        Assert.Contains("[REDACTED]", result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_RedactsSensitiveAuthorizationHeaders()
    {
        var ex = new InvalidOperationException(
            "Authorization: Bearer abc.def.ghi");

        var result = _sanitizer.Sanitize(ex, DefaultOptions());

        Assert.DoesNotContain("abc.def.ghi", result.SanitizedMessage);
        Assert.Contains("[REDACTED]", result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_RedactsConnectionStringCredentials()
    {
        var ex = new InvalidOperationException(
            "DB error: Server=sql.corp.net;Database=Sales;" +
            "User ID=admin_user;Password=SuperSecretPassword123;");

        var result = _sanitizer.Sanitize(ex, DefaultOptions());

        Assert.DoesNotContain(
            "SuperSecretPassword123",
            result.SanitizedMessage);

        Assert.DoesNotContain(
            "admin_user",
            result.SanitizedMessage);

        Assert.Contains("[REDACTED]", result.SanitizedMessage);

        // Non-secret connection details should remain available for debugging.
        Assert.Contains("Server=sql.corp.net", result.SanitizedMessage);
        Assert.Contains("Database=Sales", result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_RedactsCommonSecretKeyValues()
    {
        var ex = new InvalidOperationException(
            "Request failed with apiKey: secret_api_token_value_99999 " +
            "and client_secret: TopSecretValue123");

        var result = _sanitizer.Sanitize(ex, DefaultOptions());

        Assert.DoesNotContain(
            "secret_api_token_value_99999",
            result.SanitizedMessage);

        Assert.DoesNotContain(
            "TopSecretValue123",
            result.SanitizedMessage);

        Assert.Contains("[REDACTED]", result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_RedactsQuotedJsonSecrets()
    {
        var ex = new InvalidOperationException(
            """{"apiKey":"json-secret-value-12345","password":"my-password-123"}""");

        var result = _sanitizer.Sanitize(ex, DefaultOptions());

        Assert.DoesNotContain("json-secret-value-12345", result.SanitizedMessage);
        Assert.DoesNotContain("my-password-123", result.SanitizedMessage);
        Assert.Contains("[REDACTED]", result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_RedactsSpecificApiKeyPrefixes()
    {
        var ex = new InvalidOperationException(
            "Failed call with gsk_abcdef12345678901234567890 " +
            "and sk_98765432109876543210");

        var result = _sanitizer.Sanitize(ex, DefaultOptions());

        Assert.DoesNotContain(
            "gsk_abcdef12345678901234567890",
            result.SanitizedMessage);

        Assert.DoesNotContain(
            "sk_98765432109876543210",
            result.SanitizedMessage);

        // ProviderApiKeyRegex replaces these with [REDACTED].
        Assert.Contains("[REDACTED]", result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_RedactsJwtLikeTokens()
    {
        var jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTYifQ.signature123";

        var ex = new InvalidOperationException($"Authentication failed: {jwt}");

        var result = _sanitizer.Sanitize(ex, DefaultOptions());

        Assert.DoesNotContain(jwt, result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_RedactsUrlSignatures()
    {
        var ex = new InvalidOperationException(
            "Upload failed: https://example.test/blob?sig=very-secret-signature-123&other=value");

        var result = _sanitizer.Sanitize(ex, DefaultOptions());

        Assert.DoesNotContain(
            "very-secret-signature-123",
            result.SanitizedMessage);

        Assert.Contains("[REDACTED]", result.SanitizedMessage);
        Assert.Contains("other=value", result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_RedactsPrivateKeyBlocks()
    {
        var privateKey =
            "-----BEGIN PRIVATE KEY-----\n" +
            "FAKE-PRIVATE-KEY-CONTENT-12345\n" +
            "-----END PRIVATE KEY-----";

        var ex = new InvalidOperationException($"Key load failed: {privateKey}");

        var result = _sanitizer.Sanitize(ex, DefaultOptions());

        Assert.DoesNotContain(
            "FAKE-PRIVATE-KEY-CONTENT-12345",
            result.SanitizedMessage);

        Assert.Contains("[REDACTED]", result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_TruncatesOversizedMessages()
    {
        var ex = new InvalidOperationException(new string('A', 2500));

        var options = DefaultOptions();
        options.MaxMessageLength = 500;

        var result = _sanitizer.Sanitize(ex, options);

        Assert.Equal(500, result.SanitizedMessage.Length);
        Assert.EndsWith("... [truncated]", result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_ZeroMessageLimit_ReturnsEmptyMessage()
    {
        var ex = new InvalidOperationException("Sensitive diagnostic details");

        var options = DefaultOptions();
        options.MaxMessageLength = 0;

        var result = _sanitizer.Sanitize(ex, options);

        Assert.Empty(result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_TruncatesOversizedStackTraces()
    {
        Exception ex;

        try
        {
            throw new ArgumentNullException(
                "param",
                "Simulated trace generation");
        }
        catch (Exception caught)
        {
            ex = caught;
        }

        var options = DefaultOptions();
        options.MaxStackTraceLength = 100;

        var result = _sanitizer.Sanitize(ex, options);

        Assert.NotNull(result.SanitizedStackTrace);
        Assert.Equal(100, result.SanitizedStackTrace.Length);
        Assert.EndsWith(
            "... [truncated]",
            result.SanitizedStackTrace);
    }

    [Fact]
    public void Sanitize_RedactsSecretsInInnerExceptions()
    {
        var inner = new InvalidOperationException(
            "Inner error with Password=SecretInnerPass;");

        var outer = new ApplicationException("Outer failure", inner);

        var result = _sanitizer.Sanitize(outer, DefaultOptions());

        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            result.InnerExceptionType);

        Assert.DoesNotContain(
            "SecretInnerPass",
            result.SanitizedInnerMessage);

        Assert.Contains(
            "[REDACTED]",
            result.SanitizedInnerMessage);
    }

    [Fact]
    public void Sanitize_IncludesNestedInnerExceptionMessages()
    {
        var deepest = new InvalidOperationException(
            "Database rejected Password=DeepSecret123;");

        var middle = new ApplicationException(
            "Save operation failed",
            deepest);

        var outer = new Exception("Request failed", middle);

        var result = _sanitizer.Sanitize(outer, DefaultOptions());

        Assert.Equal(
            typeof(ApplicationException).FullName,
            result.InnerExceptionType);

        Assert.Contains(
            typeof(InvalidOperationException).FullName!,
            result.SanitizedInnerMessage);

        Assert.DoesNotContain(
            "DeepSecret123",
            result.SanitizedInnerMessage);
    }

    [Fact]
    public void Sanitize_RedactsSecretsFromStackTrace()
    {
        Exception ex;

        try
        {
            throw new InvalidOperationException(
                "Failed request with apiKey=stack-secret-value-12345");
        }
        catch (Exception caught)
        {
            ex = caught;
        }

        var result = _sanitizer.Sanitize(ex, DefaultOptions());

        Assert.DoesNotContain(
            "stack-secret-value-12345",
            result.SanitizedMessage);

        if (result.SanitizedStackTrace is not null)
        {
            Assert.DoesNotContain(
                "stack-secret-value-12345",
                result.SanitizedStackTrace);
        }
    }

    [Fact]
    public void Sanitize_ThrowsWhenExceptionIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _sanitizer.Sanitize(null!, DefaultOptions()));
    }

    [Fact]
    public void Sanitize_ThrowsWhenOptionsAreNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _sanitizer.Sanitize(
                new InvalidOperationException("Test"),
                null!));
    }
}
