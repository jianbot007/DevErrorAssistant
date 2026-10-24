namespace DevErrorAssistant;

/// <summary>
/// Configuration options for the DevErrorAssistant middleware.
/// </summary>
public class DevErrorAssistantOptions
{
    /// <summary>
    /// Gets or sets whether DevErrorAssistant middleware is active.
    /// Default is <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether detailed diagnostic details (type, message, stack trace) are shown.
    /// When <c>null</c> (default), DevErrorAssistant automatically detects the host environment:
    /// details are enabled in Development and disabled in Production / Staging.
    /// </summary>
    public bool? ShowDetailedDiagnostics { get; set; }

    /// <summary>
    /// Gets or sets the name or virtual path of the Razor view to render.
    /// Default is <c>"DevErrorPage"</c>.
    /// </summary>
    public string ViewName { get; set; } = "DevErrorPage";

    #region Future Implements: AI Provider Configuration Options

    // Future Implements:
    // Future releases will include options for configuring AI models and context sanitization:
    // public string? AiProvider { get; set; }
    // public string? ApiKey { get; set; }
    // public bool SanitizeStackPaths { get; set; } = true;

    #endregion
}
