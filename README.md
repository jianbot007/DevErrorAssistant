# DevErrorAssistant

A developer-friendly ASP.NET Core exception-handling middleware package that intercepts unhandled runtime exceptions during development and renders a clean, self-contained diagnostic error view.

Designed with a maintainable architecture, `DevErrorAssistant` includes clearly labelled **`Future Implements`** extension points for planned AI-driven root-cause explanations and suggested fixes, while operating **100% offline with zero external dependencies or API keys** in this MVP.

---

## Current MVP Scope

### Implemented Features
- **Middleware Pipeline Interception**: Catches unhandled runtime exceptions bubbling through the ASP.NET Core request pipeline and converts them to HTTP 500 responses directly without redirect loops.
- **Packaged Razor View (RCL)**: Ships the diagnostic `.cshtml` view directly inside the library using the Razor SDK (`Microsoft.NET.Sdk.Razor`). Consuming MVC applications do not need to copy any `.cshtml` files into their project.
- **Developer Diagnostics**: Displays exception type, exception message, and a collapsible native HTML5 stack trace in Development environments.
- **Safe-by-Default Production Mode**: In Production/Staging environments (or via configuration), detailed stack traces and raw error messages are automatically suppressed to protect sensitive server paths, credentials, and configuration.
- **Response-Started Protection**: Inspects `context.Response.HasStarted`. If headers or content have already streamed to the client, the middleware logs a warning and safely rethrows the exception rather than attempting an invalid response rewrite.
- **Zero External Dependencies**: Zero AI SDKs, external CSS frameworks, JavaScript libraries, CDN fonts, or telemetry calls. The error page uses system fonts and self-contained inline CSS.
- **Static Future Implements Placeholders**: The error page and data model provide dedicated sections displaying static placeholders for upcoming AI capabilities.

### Explicit Non-Goals for this MVP
- **No AI service calls**: No calls are made to OpenAI, Anthropic, Google Gemini, Ollama, or any external LLM provider.
- **No external network communication**: Works completely offline.
- **No persistent database storage**: Exception details are rendered in-flight and logged via `ILogger`.

---

## Architecture & Project Structure

The solution targets **.NET 9** (`net9.0`) and consists of three projects:

```
DevErrorAssistant/
├── DevErrorAssistant/                 # Core library & Razor Class Library (NuGet package)
│   ├── Middleware/
│   │   └── DevErrorMiddleware.cs      # Core exception-interception middleware
│   ├── Models/
│   │   └── DevErrorViewModel.cs       # Diagnostic view model with AI placeholders & sanitization
│   ├── Rendering/
│   │   ├── IDevErrorViewRenderer.cs   # View renderer abstraction
│   │   └── DevErrorViewRenderer.cs    # Composite view engine renderer with HTML fallback
│   ├── Views/
│   │   └── Shared/
│   │       └── DevErrorPage.cshtml    # Packaged Razor error view
│   ├── DevErrorAssistantOptions.cs    # Configuration options (diagnostics toggle, view name)
│   ├── DevErrorAssistantExtensions.cs # app.UseDevErrorAssistant() & services.AddDevErrorAssistant()
│   └── DevErrorAssistant.csproj       # Razor SDK project with NuGet packaging metadata
│
├── DevErrorAssistant.Demo/            # ASP.NET Core MVC demo application
│   ├── Controllers/
│   │   └── HomeController.cs          # Contains deliberate /Home/ThrowError action
│   ├── Views/Home/Index.cshtml        # UI trigger button for the test exception
│   └── Program.cs                     # Middleware registration demonstration
│
└── DevErrorAssistant.Tests/           # Automated test suite (xUnit + Microsoft.AspNetCore.TestHost)
    ├── DevErrorMiddlewareTests.cs     # End-to-end pipeline, status 500, mode toggle, and guards
    └── DevErrorViewModelTests.cs      # Model sanitization and placeholder assertions
```

### Architectural Decision: Direct Razor Class Library Conversion
Rather than splitting the project into a separate class library and a separate Razor Class Library (introducing redundant project layers and dependency coupling), `DevErrorAssistant.csproj` was converted directly into a **Razor Class Library** (`Microsoft.NET.Sdk.Razor`) referencing `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. This cleanly bundles the precompiled `.cshtml` view, the middleware, and the registration extension methods into a single unified NuGet package.

---

## Requirements

- **.NET 9 SDK** (version `9.0.100` or higher)
- Supported OS: Windows, Linux, macOS

---

## Quickstart & Registration

### 1. Register Middleware in `Program.cs`

Call `app.UseDevErrorAssistant()` at the **very beginning** of the HTTP pipeline:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add MVC services (enables Razor view discovery)
builder.Services.AddControllersWithViews();

// Optional: Register custom DevErrorAssistant options
// builder.Services.AddDevErrorAssistant(options => {
//     options.ShowDetailedDiagnostics = true;
// });

var app = builder.Build();

// CRITICAL: Register DevErrorAssistant as the first middleware in the pipeline!
// This ensures it wraps all downstream routing, authentication, and endpoint handlers.
app.UseDevErrorAssistant();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

### Middleware Pipeline Ordering Rationale

| Order | Middleware | Purpose / Interaction with DevErrorAssistant |
| :--- | :--- | :--- |
| **1 (Top)** | `app.UseDevErrorAssistant()` | Must be first so its `try/catch` surrounds all downstream middleware and captures any unhandled exceptions. |
| 2 | `app.UseHttpsRedirection()` | Handles protocol redirects. |
| 3 | `app.UseRouting()` | Matches incoming requests to route endpoints. |
| 4 | `app.UseAuthentication()` | Authenticates user credentials. Exceptions in auth handlers bubble up to DevErrorAssistant. |
| 5 | `app.UseAuthorization()` | Enforces authorization policies. |
| 6 | `app.MapControllerRoute()` | Executes controller actions (where business exceptions typically occur). |

---

## Configuration Options

You can customize `DevErrorAssistantOptions` during registration:

```csharp
app.UseDevErrorAssistant(options =>
{
    // Enable or disable the middleware
    options.Enabled = true;

    // Force detailed diagnostics on or off regardless of environment:
    // - true: Always show exception message and stack trace.
    // - false: Always hide details and show generic safe message.
    // - null (default): Automatically show details in Development, hide in Production.
    options.ShowDetailedDiagnostics = null;

    // Customize the Razor view name (defaults to "DevErrorPage")
    options.ViewName = "DevErrorPage";
});
```

---

## Development vs. Production Behavior & Security

| Feature | Development Mode (`ASPNETCORE_ENVIRONMENT=Development`) | Production Mode (`ASPNETCORE_ENVIRONMENT=Production`) |
| :--- | :--- | :--- |
| **HTTP Status** | `500 Internal Server Error` | `500 Internal Server Error` |
| **Exception Type** | Full type name (e.g., `System.InvalidOperationException`) | Safe generic title (`"An error occurred"`) |
| **Exception Message** | Original developer error message | Safe generic message advising diagnostics are disabled |
| **Stack Trace** | Full interactive, collapsible `<details>` section | **Completely omitted** (null) |
| **Sensitive Data** | Request headers, cookies, tokens, and DB connection strings are **never** included in the view model | **Completely omitted** |
| **Server Logging** | Logged server-side via `ILogger.LogError` | Logged server-side via `ILogger.LogError` |
| **AI Placeholders** | Static `Future Implements` cards rendered | Static `Future Implements` cards rendered |

---

## How to Build and Run Tests

### Build Solution
```bash
dotnet build
```

### Run Automated Tests
```bash
dotnet test
```

The automated test suite verifies:
1. Normal requests proceed without interference (`200 OK`).
2. Unhandled exceptions return `HTTP 500` with the custom error view.
3. Development mode displays exception type, message, and collapsible stack trace.
4. Production mode suppresses stack traces and displays safe generic error messages.
5. Exceptions thrown after `context.Response.HasStarted` are safely logged and rethrown without invalid response rewrites.
6. The compiled Razor view is discovered from the RCL assembly without requiring copied `.cshtml` files.
7. Explicit options override environment defaults.
8. Data model factory produces valid static `Future Implements` placeholders.

---

## Testing the Demo Application

1. Start the demo application:
   ```bash
   dotnet run --project DevErrorAssistant.Demo
   ```
2. Open your browser and navigate to the application URL (e.g., `http://localhost:5248`).
3. Click the red **"Trigger Test Exception"** button on the home page or navigate directly to:
   ```
   http://localhost:5248/Home/ThrowError
   ```
4. Observe the custom error page with:
   - Header badge: `HTTP 500 — Unhandled Exception`
   - Active status: `Development Mode Active`
   - Exception type: `System.InvalidOperationException`
   - Collapsible stack trace section
   - `Future Implements — AI Exception Insights` card with static placeholders

---

## Creating and Consuming the NuGet Package Locally

### 1. Build the `.nupkg` Package
```bash
dotnet pack -c Release DevErrorAssistant/DevErrorAssistant.csproj -o ./artifacts/packages
```

This creates:
- `artifacts/packages/DevErrorAssistant.1.0.0-mvp.nupkg`
- `artifacts/packages/DevErrorAssistant.1.0.0-mvp.snupkg` (symbols)

### 2. Verify Package Contents
You can verify that the `.nupkg` archive contains `DevErrorAssistant.dll` and `DevErrorAssistant.xml`:
```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::OpenRead("artifacts/packages/DevErrorAssistant.1.0.0-mvp.nupkg").Entries | Select-Object FullName
```

### 3. Install in Another Project via Local Feed
To consume the package in another ASP.NET Core project on your local machine:

```bash
# Add a local package source pointing to the output directory
dotnet nuget add source "D:\Dot net core projects\DevErrorAssistant\artifacts\packages" --name LocalDevPackages

# Add the package to your project
dotnet add package DevErrorAssistant --version 1.0.0-mvp
```

---

## Future Implements (Planned AI Roadmap)

The `DevErrorAssistant` architecture has been designed with explicit extension points for upcoming AI-assisted troubleshooting capabilities:

1. **AI Exception Explanation Service**:
   - An asynchronous analysis provider that inspects exception details, inner exceptions, and stack frames to generate a plain-English explanation of why the failure occurred.
2. **Contextual Solution Suggestions**:
   - Actionable code recommendations and architectural fixes tailored to the detected exception and framework version.
3. **Flexible AI Providers**:
   - Pluggable provider support (e.g., local Ollama for privacy-sensitive offline development, or remote providers like Google Gemini, Claude, or OpenAI).
4. **Diagnostic Context Sanitizer**:
   - An automated scrubbing engine to strip connection strings, API keys, passwords, personal data, and local file paths before sending exception context to any external AI provider.

> [!NOTE]
> None of the above AI features are active in this MVP. All explanation and solution fields currently return static placeholder strings (`Future Implements — ...`).
