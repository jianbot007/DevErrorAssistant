using System.Net;
using DevErrorAssistant.Middleware;
using DevErrorAssistant.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DevErrorAssistant.Tests;

public class DevErrorMiddlewareTests
{
    [Fact]
    public async Task NormalRequest_ProceedsSuccessfully_Returns200Ok()
    {
        // 1. Verify a normal request proceeds successfully
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseDevErrorAssistant();
                    app.Run(async context =>
                    {
                        context.Response.StatusCode = 200;
                        await context.Response.WriteAsync("Success: Pipeline functional");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Success: Pipeline functional", content);
    }

    [Fact]
    public async Task UnhandledException_InDevelopment_Returns500_AndRendersDiagnosticsAndPlaceholders()
    {
        // 2, 3, 4: Unhandled exception results in HTTP 500, custom page rendered, type & message appear in dev mode
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseEnvironment("Development");
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllersWithViews();
                    services.AddDevErrorAssistant();
                });
                webBuilder.Configure(app =>
                {
                    app.UseDevErrorAssistant();
                    app.Run(context =>
                    {
                        throw new InvalidOperationException("Demo simulated runtime failure in development.");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("/trigger-error");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());

        var content = await response.Content.ReadAsStringAsync();
        var decodedContent = WebUtility.HtmlDecode(content);

        // Check header & brand
        Assert.Contains("DevErrorAssistant", decodedContent);
        Assert.Contains("HTTP 500 — Unhandled Exception", decodedContent);

        // Check diagnostic details
        Assert.Contains("System.InvalidOperationException", decodedContent);
        Assert.Contains("Demo simulated runtime failure in development.", decodedContent);
        Assert.Contains("Stack Trace", decodedContent);

        // Check Future Implements AI placeholders
        Assert.Contains("Future Implements — AI Exception Insights", decodedContent);
        Assert.Contains("Future Implements — AI-generated explanation of this exception.", decodedContent);
        Assert.Contains("Future Implements — AI-generated suggestions for resolving this exception.", decodedContent);
        Assert.Contains("Not implemented in this MVP.", decodedContent);
    }

    [Fact]
    public async Task UnhandledException_InProduction_HidesDiagnosticDetails()
    {
        // 5. Diagnostic details are hidden in production mode
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseEnvironment("Production");
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllersWithViews();
                    services.AddDevErrorAssistant();
                });
                webBuilder.Configure(app =>
                {
                    app.UseDevErrorAssistant();
                    app.Run(context =>
                    {
                        throw new InvalidOperationException("Internal secret connection details: User=db_admin;Password=super_secret");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("/trigger-error");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var decodedContent = WebUtility.HtmlDecode(content);

        // Must display branding and safe generic message
        Assert.Contains("DevErrorAssistant", decodedContent);
        Assert.Contains("Detailed diagnostic information is disabled in this environment", decodedContent);

        // Sensitive exception message and stack trace must NOT be leaked
        Assert.DoesNotContain("db_admin", decodedContent);
        Assert.DoesNotContain("super_secret", decodedContent);

        // Future Implements placeholders remain visible
        Assert.Contains("Future Implements — AI Exception Insights", decodedContent);
        Assert.Contains("Future Implements — AI-generated explanation of this exception.", decodedContent);
        Assert.Contains("Future Implements — AI-generated suggestions for resolving this exception.", decodedContent);
        Assert.Contains("Not implemented in this MVP.", decodedContent);
    }

    [Fact]
    public async Task UnhandledException_WhenResponseHasStarted_RethrowsWithoutModifyingResponse()
    {
        // 6, 7: Response not started vs response has started
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseEnvironment("Development");
                webBuilder.UseTestServer();
                webBuilder.Configure(app =>
                {
                    app.UseDevErrorAssistant();
                    app.Run(async context =>
                    {
                        // Start the response stream
                        context.Response.StatusCode = 200;
                        await context.Response.WriteAsync("Stream has started...");
                        await context.Response.Body.FlushAsync();

                        // Now throw after response has already started
                        throw new InvalidOperationException("Exception thrown after response started.");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // The middleware will rethrow because context.Response.HasStarted is true.
        // HttpClient will encounter a stream failure caused by the underlying InvalidOperationException.
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await client.GetAsync("/started-error");
        });

        // Verify that the rethrown exception originates from our downstream exception
        var rootCause = ex.GetBaseException();
        Assert.IsType<InvalidOperationException>(rootCause);
        Assert.Equal("Exception thrown after response started.", rootCause.Message);
    }

    [Fact]
    public async Task Options_ShowDetailedDiagnosticsExplicitFalse_SuppressesDetailsInDevelopment()
    {
        // Verify explicit configuration overrides environment
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseEnvironment("Development");
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllersWithViews();
                    services.AddDevErrorAssistant();
                });
                webBuilder.Configure(app =>
                {
                    app.UseDevErrorAssistant(options =>
                    {
                        options.ShowDetailedDiagnostics = false;
                    });
                    app.Run(context =>
                    {
                        throw new InvalidOperationException("Detailed error that should be hidden.");
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.GetAsync("/explicit-suppression");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var decodedContent = WebUtility.HtmlDecode(content);

        Assert.DoesNotContain("Detailed error that should be hidden.", decodedContent);
        Assert.Contains("Detailed diagnostic information is disabled in this environment", decodedContent);
    }

    [Fact]
    public async Task RazorView_IsDiscoveredFromRazorClassLibraryAssembly()
    {
        // 8. The Razor view is available through the packaged library rather than requiring a copied file
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseEnvironment("Development");
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddDevErrorAssistant();
                });
                webBuilder.Configure(app =>
                {
                    app.UseDevErrorAssistant();
                    app.Run(context =>
                    {
                        throw new ApplicationException("Testing Razor view resolution from RCL.");
                    });
                });
            })
            .StartAsync();

        // Verify that ICompositeViewEngine can locate the compiled view in DevErrorAssistant RCL
        var viewEngine = host.Services.GetRequiredService<ICompositeViewEngine>();
        var viewResult = viewEngine.GetView(executingFilePath: null, viewPath: "/Views/Shared/DevErrorPage.cshtml", isMainPage: false);

        Assert.True(viewResult.Success, "The DevErrorPage.cshtml Razor view must be discoverable from the RCL assembly without copying files.");
        Assert.NotNull(viewResult.View);

        // Also verify execution through HTTP pipeline
        var client = host.GetTestClient();
        var response = await client.GetAsync("/test-view");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Testing Razor view resolution from RCL.", content);
    }
}
