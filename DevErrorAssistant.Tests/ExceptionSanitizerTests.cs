using DevErrorAssistant.Ai;
using Xunit;

namespace DevErrorAssistant.Tests;

public class ExceptionSanitizerTests
{
    private readonly ExceptionSanitizer _sanitizer = new();

    [Fact]
    public void Sanitize_RedactsBearerTokens()
    {
        var ex = new InvalidOperationException("Failed auth call: Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e30.signature");
        var options = new ExceptionSanitizationOptions();

        var result = _sanitizer.Sanitize(ex, options);

        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", result.SanitizedMessage);
        Assert.Contains("Bearer [REDACTED_TOKEN]", result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_RedactsConnectionStringCredentials()
    {
        var ex = new InvalidOperationException("DB error: Server=sql.corp.net;Database=Sales;User ID=admin_user;Password=SuperSecretPassword123;");
        var options = new ExceptionSanitizationOptions();

        var result = _sanitizer.Sanitize(ex, options);

        Assert.DoesNotContain("SuperSecretPassword123", result.SanitizedMessage);
        Assert.DoesNotContain("admin_user", result.SanitizedMessage);
        Assert.Contains("[REDACTED]", result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_RedactsCommonSecretKeyValues()
    {
        var ex = new InvalidOperationException("Request failed with apiKey: secret_api_token_value_99999 and client_secret: TopSecretValue123");
        var options = new ExceptionSanitizationOptions();

        var result = _sanitizer.Sanitize(ex, options);

        Assert.DoesNotContain("secret_api_token_value_99999", result.SanitizedMessage);
        Assert.DoesNotContain("TopSecretValue123", result.SanitizedMessage);
        Assert.Contains("[REDACTED]", result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_RedactsSpecificApiKeyPrefixes()
    {
        var ex = new InvalidOperationException("Failed call with gsk_abcdef12345678901234567890 and sk_98765432109876543210");
        var options = new ExceptionSanitizationOptions();

        var result = _sanitizer.Sanitize(ex, options);

        Assert.DoesNotContain("gsk_abcdef12345678901234567890", result.SanitizedMessage);
        Assert.DoesNotContain("sk_98765432109876543210", result.SanitizedMessage);
        Assert.Contains("[REDACTED_KEY]", result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_TruncatesOversizedMessages()
    {
        var oversizedMessage = new string('A', 2500);
        var ex = new InvalidOperationException(oversizedMessage);
        var options = new ExceptionSanitizationOptions { MaxMessageLength = 500 };

        var result = _sanitizer.Sanitize(ex, options);

        Assert.True(result.SanitizedMessage.Length <= 500);
        Assert.EndsWith("... [truncated]", result.SanitizedMessage);
    }

    [Fact]
    public void Sanitize_TruncatesOversizedStackTraces()
    {
        Exception ex;
        try
        {
            throw new ArgumentNullException("param", "Simulated trace generation");
        }
        catch (Exception caught)
        {
            ex = caught;
        }

        var options = new ExceptionSanitizationOptions { MaxStackTraceLength = 100 };

        var result = _sanitizer.Sanitize(ex, options);

        Assert.NotNull(result.SanitizedStackTrace);
        Assert.True(result.SanitizedStackTrace.Length <= 100);
        Assert.EndsWith("... [truncated]", result.SanitizedStackTrace);
    }

    [Fact]
    public void Sanitize_SanitizesInnerExceptions()
    {
        var inner = new InvalidOperationException("Inner error with Password=SecretInnerPass;");
        var outer = new ApplicationException("Outer failure", inner);
        var options = new ExceptionSanitizationOptions();

        var result = _sanitizer.Sanitize(outer, options);

        Assert.Equal(typeof(InvalidOperationException).FullName, result.InnerExceptionType);
        Assert.DoesNotContain("SecretInnerPass", result.SanitizedInnerMessage);
        Assert.Contains("[REDACTED]", result.SanitizedInnerMessage);
    }
}
