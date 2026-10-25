using DevErrorAssistant.Models;
using Microsoft.AspNetCore.Http;

namespace DevErrorAssistant.Rendering;

/// <summary>
/// Defines a contract for rendering the DevErrorAssistant error page to the HTTP response.
/// </summary>
public interface IDevErrorViewRenderer
{
    /// <summary>
    /// Renders the error page view to the HTTP response.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="model">The diagnostic error view model.</param>
    /// <param name="options">The DevErrorAssistant configuration options.</param>
    Task RenderAsync(HttpContext context, DevErrorViewModel model, DevErrorAssistantOptions options);
}
