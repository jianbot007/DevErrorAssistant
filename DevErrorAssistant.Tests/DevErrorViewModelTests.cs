using DevErrorAssistant.Models;
using Xunit;

namespace DevErrorAssistant.Tests;

public class DevErrorViewModelTests
{
    [Fact]
    public void FromException_WhenShowDetailsIsTrue_PopulatesDiagnosticFields()
    {
        // Arrange
        Exception exception;
        try
        {
            throw new InvalidOperationException("Operation failed due to invalid state.");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Act
        var model = DevErrorViewModel.FromException(exception, showDetails: true);

        // Assert
        Assert.True(model.ShowDetails);
        Assert.Equal(typeof(InvalidOperationException).FullName, model.ExceptionType);
        Assert.Equal("Operation failed due to invalid state.", model.ExceptionMessage);
        Assert.NotNull(model.StackTrace);
        Assert.Contains(nameof(FromException_WhenShowDetailsIsTrue_PopulatesDiagnosticFields), model.StackTrace);

        // Future Implements placeholders
        Assert.Equal("Future Implements — AI-generated explanation of this exception.", model.Explanation);
        Assert.Equal("Future Implements — AI-generated suggestions for resolving this exception.", model.PossibleSolution);
        Assert.Equal("Not implemented in this MVP.", model.AiStatus);
    }

    [Fact]
    public void FromException_WhenShowDetailsIsFalse_SanitizesSensitiveDiagnostics()
    {
        // Arrange
        Exception exception;
        try
        {
            throw new ArgumentException("Sensitive database credentials missing in connection string: Server=secret;User=admin;Password=123");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Act
        var model = DevErrorViewModel.FromException(exception, showDetails: false);

        // Assert
        Assert.False(model.ShowDetails);
        Assert.Null(model.StackTrace);
        Assert.DoesNotContain("secret", model.ExceptionMessage);
        Assert.DoesNotContain("admin", model.ExceptionMessage);
        Assert.DoesNotContain("Password", model.ExceptionMessage);
        Assert.Equal("An error occurred", model.ExceptionType);
        Assert.Contains("Detailed diagnostic information is disabled in this environment.", model.ExceptionMessage);

        // Future Implements placeholders remain present
        Assert.Equal("Future Implements — AI-generated explanation of this exception.", model.Explanation);
        Assert.Equal("Future Implements — AI-generated suggestions for resolving this exception.", model.PossibleSolution);
        Assert.Equal("Not implemented in this MVP.", model.AiStatus);
    }

    [Fact]
    public void FromException_WithNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => DevErrorViewModel.FromException(null!, showDetails: true));
    }
}
