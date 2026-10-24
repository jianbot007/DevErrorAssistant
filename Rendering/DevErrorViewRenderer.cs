using System.Net;
using System.Text.Encodings.Web;
using DevErrorAssistant.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevErrorAssistant.Rendering;

/// <summary>
/// Default implementation of <see cref="IDevErrorViewRenderer"/> that renders Razor views from the
/// Razor Class Library, with an integrated HTML fallback if MVC view services are unavailable.
/// </summary>
public class DevErrorViewRenderer : IDevErrorViewRenderer
{
    private readonly ILogger<DevErrorViewRenderer> _logger;

    public DevErrorViewRenderer(ILogger<DevErrorViewRenderer> logger)
    {
        _logger = logger;
    }

    public async Task RenderAsync(HttpContext context, DevErrorViewModel model, DevErrorAssistantOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(options);

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "text/html; charset=utf-8";

        var viewEngine = context.RequestServices.GetService<ICompositeViewEngine>();
        if (viewEngine != null)
        {
            try
            {
                var actionContext = CreateActionContext(context);
                var viewResult = FindView(viewEngine, actionContext, options.ViewName);

                if (viewResult.Success && viewResult.View != null)
                {
                    var tempDataProvider = context.RequestServices.GetService<ITempDataProvider>()
                                           ?? new EmptyTempDataProvider();

                    var viewData = new ViewDataDictionary<DevErrorViewModel>(
                        new EmptyModelMetadataProvider(),
                        new ModelStateDictionary())
                    {
                        Model = model
                    };

                    await using var writer = new StringWriter();
                    var viewContext = new ViewContext(
                        actionContext,
                        viewResult.View,
                        viewData,
                        new TempDataDictionary(context, tempDataProvider),
                        writer,
                        new HtmlHelperOptions());

                    await viewResult.View.RenderAsync(viewContext);
                    await context.Response.WriteAsync(writer.ToString());
                    return;
                }

                _logger.LogWarning("DevErrorAssistant could not locate Razor view '{ViewName}'. Falling back to built-in HTML renderer.", options.ViewName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to render Razor view '{ViewName}'. Using built-in HTML renderer.", options.ViewName);
            }
        }
        else
        {
            _logger.LogDebug("ICompositeViewEngine service is not registered. Using built-in HTML renderer.");
        }

        // Fallback HTML output when MVC Razor view engine is not registered or view lookup fails
        await RenderFallbackHtmlAsync(context, model);
    }

    private static ActionContext CreateActionContext(HttpContext context)
    {
        var routeData = context.GetRouteData() ?? new RouteData();
        return new ActionContext(context, routeData, new ActionDescriptor());
    }

    private static ViewEngineResult FindView(ICompositeViewEngine viewEngine, ActionContext actionContext, string viewName)
    {
        // 1. Try FindView (searches standard view search locations including RCL Views/Shared)
        var result = viewEngine.FindView(actionContext, viewName, isMainPage: false);
        if (result.Success)
        {
            return result;
        }

        // 2. Try exact path if it looks like a path
        if (viewName.StartsWith("~/") || viewName.StartsWith("/"))
        {
            result = viewEngine.GetView(executingFilePath: null, viewPath: viewName, isMainPage: false);
            if (result.Success)
            {
                return result;
            }
        }

        // 3. Try standard RCL shared view paths
        result = viewEngine.GetView(executingFilePath: null, viewPath: $"/Views/Shared/{viewName}.cshtml", isMainPage: false);
        if (result.Success)
        {
            return result;
        }

        return viewEngine.GetView(executingFilePath: null, viewPath: $"/Views/{viewName}.cshtml", isMainPage: false);
    }

    private static async Task RenderFallbackHtmlAsync(HttpContext context, DevErrorViewModel model)
    {
        var encoder = HtmlEncoder.Default;
        var encodedType = encoder.Encode(model.ExceptionType);
        var encodedMessage = encoder.Encode(model.ExceptionMessage);
        var encodedExplanation = encoder.Encode(model.Explanation);
        var encodedSolution = encoder.Encode(model.PossibleSolution);
        var encodedAiStatus = encoder.Encode(model.AiStatus);

        var stackTraceHtml = string.Empty;
        if (model.ShowDetails && !string.IsNullOrWhiteSpace(model.StackTrace))
        {
            var encodedStack = encoder.Encode(model.StackTrace);
            stackTraceHtml = $@"
                <details open style=""margin-top: 1rem; border: 1px solid #334155; border-radius: 6px; overflow: hidden; background-color: #090d16;"">
                    <summary style=""padding: 0.85rem 1rem; background-color: rgba(255, 255, 255, 0.03); cursor: pointer; font-weight: 600; color: #94a3b8;"">
                        <span>Stack Trace</span>
                    </summary>
                    <pre style=""padding: 1rem; overflow-x: auto; font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; font-size: 0.85rem; color: #cbd5e1; white-space: pre-wrap; word-break: break-all;"">{encodedStack}</pre>
                </details>";
        }
        else if (!model.ShowDetails)
        {
            stackTraceHtml = @"
                <div style=""background-color: rgba(148, 163, 184, 0.1); border: 1px solid #334155; border-radius: 6px; padding: 0.85rem 1rem; color: #94a3b8; font-size: 0.9rem; margin-top: 1rem;"">
                    <strong>Notice:</strong> Detailed stack trace and exception diagnostics are disabled in this environment for security.
                </div>";
        }

        var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>DevErrorAssistant — {encodedType}</title>
    <style>
        body {{ background-color: #0f172a; color: #f8fafc; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; padding: 2rem 1rem; margin: 0; }}
        .container {{ max-width: 960px; margin: 0 auto; }}
        header {{ display: flex; align-items: center; justify-content: space-between; padding-bottom: 1.5rem; margin-bottom: 2rem; border-bottom: 1px solid #334155; flex-wrap: wrap; gap: 1rem; }}
        .brand {{ display: flex; align-items: center; gap: 0.75rem; }}
        .brand-badge {{ background-color: #ef4444; color: #fff; font-size: 0.75rem; font-weight: 700; padding: 0.25rem 0.6rem; border-radius: 4px; text-transform: uppercase; }}
        .status-badge {{ padding: 0.35rem 0.75rem; background-color: rgba(239, 68, 68, 0.12); color: #ef4444; border: 1px solid rgba(239, 68, 68, 0.3); border-radius: 9999px; font-size: 0.85rem; font-weight: 600; }}
        .card {{ background-color: #1e293b; border: 1px solid #334155; border-radius: 8px; padding: 1.5rem; margin-bottom: 1.5rem; }}
        .card-title {{ font-size: 1.1rem; font-weight: 600; margin-bottom: 1rem; }}
        .exception-type {{ font-family: monospace; font-size: 1.05rem; font-weight: 700; color: #ef4444; margin-bottom: 0.5rem; word-break: break-all; }}
        .exception-message {{ font-size: 1rem; background-color: #090d16; border: 1px solid #1e293b; padding: 1rem; border-radius: 6px; white-space: pre-wrap; font-family: monospace; }}
        .future-section {{ border: 1px solid rgba(56, 189, 248, 0.3); }}
        .future-field {{ margin-bottom: 1.25rem; }}
        .future-label {{ font-size: 0.85rem; font-weight: 600; color: #38bdf8; margin-bottom: 0.35rem; text-transform: uppercase; }}
        .future-value {{ background-color: #090d16; border: 1px dashed rgba(56, 189, 248, 0.25); padding: 0.85rem 1rem; border-radius: 6px; color: #94a3b8; font-style: italic; }}
    </style>
</head>
<body>
    <div class=""container"">
        <header>
            <div class=""brand"">
                <span class=""brand-badge"">MVP</span>
                <h1 style=""font-size: 1.4rem; margin: 0;"">DevErrorAssistant</h1>
            </div>
            <div class=""status-badge"">HTTP 500 — Unhandled Exception</div>
        </header>

        <section class=""card"">
            <div class=""card-title"">Runtime Diagnostic Details</div>
            <div class=""exception-type"">{encodedType}</div>
            <div class=""exception-message"">{encodedMessage}</div>
            {stackTraceHtml}
        </section>

        <section class=""card future-section"">
            <div class=""card-title"" style=""color: #38bdf8;"">Future Implements — AI Exception Insights</div>
            <div class=""future-field"">
                <div class=""future-label"">English Explanation</div>
                <div class=""future-value"">{encodedExplanation}</div>
            </div>
            <div class=""future-field"">
                <div class=""future-label"">Possible Solution</div>
                <div class=""future-value"">{encodedSolution}</div>
            </div>
            <div style=""margin-top: 1rem; padding-top: 1rem; border-top: 1px solid #334155; font-size: 0.85rem; color: #64748b;"">
                AI Status: <span style=""background: rgba(100, 116, 139, 0.2); color: #cbd5e1; padding: 0.15rem 0.5rem; border-radius: 4px;"">{encodedAiStatus}</span>
            </div>
        </section>
    </div>
</body>
</html>";

        await context.Response.WriteAsync(html);
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
