using DevErrorAssistant;
using DevErrorAssistant.Ai;
using DevErrorAssistant.Middleware;
using DevErrorAssistant.Rendering;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for registering DevErrorAssistant middleware and services.
/// </summary>
public static class DevErrorAssistantExtensions
{
    /// <summary>
    /// Adds DevErrorAssistant diagnostic and AI explanation services to the service collection.
    /// Calling this is optional; <c>app.UseDevErrorAssistant()</c> functions with sensible defaults out of the box.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for <see cref="DevErrorAssistantOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDevErrorAssistant(
        this IServiceCollection services,
        Action<DevErrorAssistantOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IDevErrorViewRenderer, DevErrorViewRenderer>();
        services.TryAddSingleton<IExceptionSanitizer, ExceptionSanitizer>();
        services.TryAddSingleton<IAiExplanationProvider, GroqAiExplanationProvider>();
        services.TryAddSingleton<IExceptionExplanationService, ExceptionExplanationService>();

        services.AddHttpClient(GroqAiExplanationProvider.HttpClientName);

        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<DevErrorAssistantOptions>();
        }

        // Ensure the RCL assembly is registered as an ApplicationPart for Razor view discovery
        services.AddControllersWithViews()
                .AddApplicationPart(typeof(DevErrorMiddleware).Assembly);

        return services;
    }

    /// <summary>
    /// Adds the <see cref="DevErrorMiddleware"/> to the application's request pipeline.
    /// <para>
    /// <strong>Recommended Placement:</strong> Call this at the very beginning of the pipeline
    /// (before routing, authentication, authorization, and endpoints) so that exceptions thrown
    /// in any subsequent middleware or controllers are caught and rendered.
    /// </para>
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseDevErrorAssistant(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        EnsureApplicationPartRegistered(app);
        return app.UseMiddleware<DevErrorMiddleware>();
    }

    /// <summary>
    /// Adds the <see cref="DevErrorMiddleware"/> to the application's request pipeline with explicit options.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configureOptions">An action to configure <see cref="DevErrorAssistantOptions"/>.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseDevErrorAssistant(
        this IApplicationBuilder app,
        Action<DevErrorAssistantOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configureOptions);
        EnsureApplicationPartRegistered(app);

        var options = new DevErrorAssistantOptions();
        configureOptions(options);

        return app.UseMiddleware<DevErrorMiddleware>(Options.Create(options));
    }

    private static void EnsureApplicationPartRegistered(IApplicationBuilder app)
    {
        var partManager = app.ApplicationServices.GetService<ApplicationPartManager>();
        if (partManager != null)
        {
            var currentAssembly = typeof(DevErrorMiddleware).Assembly;
            if (!partManager.ApplicationParts.OfType<AssemblyPart>().Any(p => p.Assembly == currentAssembly))
            {
                partManager.ApplicationParts.Add(new AssemblyPart(currentAssembly));
            }
        }
    }
}
