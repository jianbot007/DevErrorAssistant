using DevErrorAssistant.Ai;

namespace DevErrorAssistant;

/// <summary>
/// Configuration options for the DevErrorAssistant middleware and diagnostic services.
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

    /// <summary>
    /// Gets or sets configuration options for AI-powered exception explanations.
    /// </summary>
    public DevErrorAiOptions Ai { get; set; } = new();
}
