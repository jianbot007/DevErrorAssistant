using DevErrorAssistant.Ai;
using DevErrorAssistant.Models;
using DevErrorAssistant.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevErrorAssistant.Middleware;

/// <summary>
/// ASP.NET Core middleware that catches unhandled pipeline exceptions and directly renders
/// a diagnostic error view with optional AI-powered exception analysis.
/// </summary>
public class DevErrorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DevErrorMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private readonly DevErrorAssistantOptions _options;
    private readonly IDevErrorViewRenderer _fallbackRenderer;
    private readonly IExceptionExplanationService? _fallbackExplanationService;

    public DevErrorMiddleware(
        RequestDelegate next,
        ILogger<DevErrorMiddleware> logger,
        IHostEnvironment environment,
        IOptions<DevErrorAssistantOptions>? options = null,
        IDevErrorViewRenderer? viewRenderer = null,
        IExceptionExplanationService? explanationService = null)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _options = options?.Value ?? new DevErrorAssistantOptions();
        _fallbackRenderer = viewRenderer ?? new DevErrorViewRenderer(
            LoggerFactory.Create(builder => { }).CreateLogger<DevErrorViewRenderer>());
        _fallbackExplanationService = explanationService;
    }

    /// <summary>
    /// Invokes the middleware in the ASP.NET Core request pipeline.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DevErrorAssistant caught an unhandled exception for request '{Path}'.", context.Request.Path);

            // Guard: If response headers or body have already started streaming to the client,
            // we cannot replace the response. Log a warning and rethrow the exception.
            if (context.Response.HasStarted)
            {
                _logger.LogWarning("The HTTP response has already started. DevErrorAssistant cannot modify headers or render the error page. Rethrowing exception.");
                throw;
            }

            try
            {
                // In development, display exception details; in production/staging, hide details for security.
                var showDetails = _options.ShowDetailedDiagnostics ?? _environment.IsDevelopment();
                var model = DevErrorViewModel.FromException(ex, showDetails);

                // Optional AI explanation: Only invoked if enabled in configuration
                if (_options.Ai.Enabled)
                {
                    try
                    {
                        var explanationService = context.RequestServices.GetService<IExceptionExplanationService>()
                                                 ?? _fallbackExplanationService;

                        if (explanationService != null)
                        {
                            var aiResult = await explanationService.ExplainAsync(ex, _options, context.RequestAborted);
                            model.ApplyAiResult(aiResult);
                        }
                    }
                    catch (Exception aiEx)
                    {
                        _logger.LogWarning(aiEx, "AI explanation generation failed unexpectedly. Error page will still render.");
                        model.ApplyAiResult(AiExplanationResult.Unavailable("AI explanation unavailable: An unexpected error occurred."));
                    }
                }

                var renderer = context.RequestServices.GetService<IDevErrorViewRenderer>() ?? _fallbackRenderer;
                await renderer.RenderAsync(context, model, _options);
            }
            catch (Exception renderEx)
            {
                _logger.LogError(renderEx, "An error occurred while attempting to render the DevErrorAssistant error page.");
                throw;
            }
        }
    }
}
