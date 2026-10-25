# DevErrorAssistant

A developer-friendly ASP.NET Core exception-handling middleware package that intercepts unhandled runtime exceptions during development and renders a clean, self-contained diagnostic error view with optional **Groq AI-powered explanations and troubleshooting suggestions**.

Operates **safely by default**: In production, detailed stack traces are suppressed. In development, you get full diagnostic insights and optional, opt-in AI root-cause analysis using the Groq Chat Completions API (`llama-3.3-70b-versatile`).

---

## Features

- **Middleware Pipeline Interception**: Catches unhandled runtime exceptions bubbling through the ASP.NET Core request pipeline and converts them to HTTP 500 responses directly without redirect loops.
- **Packaged Razor View (RCL)**: Ships the diagnostic `.cshtml` view directly inside the library using the Razor SDK (`Microsoft.NET.Sdk.Razor`). Consuming MVC applications do not need to copy any `.cshtml` files into their project.
- **Developer Diagnostics**: Displays exception type, exception message, and a collapsible native HTML5 stack trace in Development environments.
- **Optional Groq AI Analysis**: Provides plain-English root-cause explanations, likely causes, recommended investigation steps, and code solutions using Groq's official OpenAI-compatible endpoint.
- **Graceful Degradation**: If AI is disabled or no API key is configured, the error page renders standard diagnostics with a clear status notice. The host application **never crashes** due to AI network drops or missing keys.
- **Context Sanitization**: Scrubs connection strings, passwords, bearer tokens, and API keys, and bounds message/stack-trace length before sending context to the AI provider.
- **Safe-by-Default Production Mode**: In Production/Staging environments (or via configuration), detailed stack traces and raw error messages are automatically suppressed to protect sensitive server paths, credentials, and configuration.
- **Response-Started Protection**: Inspects `context.Response.HasStarted`. If headers or content have already streamed to the client, the middleware logs a warning and safely rethrows the exception rather than attempting an invalid response rewrite.
- **XSS-Safe Rendering**: All dynamic exception text and AI suggestions are HTML-encoded by default.

---

## Architecture & Project Structure

The solution targets **.NET 9** (`net9.0`) and consists of three projects:

```
DevErrorAssistant/
├── DevErrorAssistant/                 # Core library & Razor Class Library (NuGet package)
│   ├── Ai/
│   │   ├── IAiExplanationProvider.cs       # Extensible provider contract (Groq, etc.)
│   │   ├── GroqAiExplanationProvider.cs    # Official Groq Chat Completions HTTP implementation
│   │   ├── IExceptionExplanationService.cs # AI coordination & fault-tolerance service
│   │   ├── ExceptionExplanationService.cs  # Key resolution, dispatch & graceful fallback
│   │   ├── IExceptionSanitizer.cs          # Redaction & truncation interface
│   │   ├── ExceptionSanitizer.cs           # Regex scrubbing (tokens, connection strings, keys)
│   │   ├── SanitizedExceptionContext.cs    # Sanitized DTO passed to AI providers
│   │   ├── AiExplanationResult.cs          # Structured response (summary, causes, fixes)
│   │   └── DevErrorAiOptions.cs            # AI configuration (Provider, Model, Timeout, etc.)
│   ├── Middleware/
│   │   └── DevErrorMiddleware.cs      # Core exception-interception middleware
│   ├── Models/
│   │   └── DevErrorViewModel.cs       # Diagnostic view model with AI properties & sanitization
│   ├── Rendering/
│   │   ├── IDevErrorViewRenderer.cs   # View renderer abstraction
│   │   └── DevErrorViewRenderer.cs    # Composite view engine renderer with HTML fallback
│   ├── Views/Shared/
│   │   └── DevErrorPage.cshtml        # Packaged Razor error view
│   ├── DevErrorAssistantOptions.cs    # Configuration options (diagnostics toggle, view name)
│   ├── DevErrorAssistantExtensions.cs # app.UseDevErrorAssistant() & services.AddDevErrorAssistant()
│   └── DevErrorAssistant.csproj       # Razor SDK project with NuGet packaging metadata
│
├── DevErrorAssistant.Demo/            # ASP.NET Core MVC demo application
│   ├── Controllers/
│   │   └── HomeController.cs          # Contains deliberate /Home/ThrowError action
│   ├── Views/Home/Index.cshtml        # UI trigger button and User Secrets guide
│   └── Program.cs                     # Middleware registration demonstration
│
└── DevErrorAssistant.Tests/           # Automated test suite (30 unit & integration tests)
    ├── DevErrorMiddlewareTests.cs     # Pipeline, diagnostics, production mode, AI & XSS tests
    ├── DevErrorViewModelTests.cs      # Model sanitization and placeholder assertions
    ├── ExceptionSanitizerTests.cs     # Token, connection string, password redaction tests
    ├── GroqAiExplanationProviderTests.cs # Groq HTTP tests (401, 429, 500, timeout, invalid JSON)
    ├── ExceptionExplanationServiceTests.cs # Key resolution and provider dispatch tests
    └── FakeHttpMessageHandler.cs      # Deterministic HTTP test double
```

---

## Requirements

- **.NET 9 SDK** (version `9.0.100` or higher)
- Supported OS: Windows, Linux, macOS

---

## Installation & Setup

### 1. Register Services and Middleware in `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add MVC services (enables Razor view discovery)
builder.Services.AddControllersWithViews();

// Register DevErrorAssistant services (registers HttpClient, sanitizer, AI providers)
builder.Services.AddDevErrorAssistant(options =>
{
    // Opt-in to AI explanations (default is false)
    options.Ai.Enabled = builder.Environment.IsDevelopment();

    // Model name (default is "llama-3.3-70b-versatile")
    options.Ai.Model = "llama-3.3-70b-versatile";

    // Request timeout for AI provider (default is 10 seconds)
    options.Ai.Timeout = TimeSpan.FromSeconds(10);

    // Note: The Groq API key is resolved automatically at runtime
    // from configuration key "Groq:ApiKey" (or options.Ai.ApiKey).
});

var app = builder.Build();

// CRITICAL: Register DevErrorAssistant as the FIRST middleware in the pipeline!
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
| **1 (Top)** | `app.UseDevErrorAssistant()` | Must be first so its `try/catch` surrounds all downstream middleware and captures unhandled exceptions. |
| 2 | `app.UseHttpsRedirection()` | Handles protocol redirects. |
| 3 | `app.UseRouting()` | Matches incoming requests to route endpoints. |
| 4 | `app.UseAuthentication()` | Authenticates user credentials. Exceptions in auth handlers bubble up to DevErrorAssistant. |
| 5 | `app.UseAuthorization()` | Enforces authorization policies. |
| 6 | `app.MapControllerRoute()` | Executes controller actions (where business exceptions typically occur). |

---

## Configuring Groq API Key via .NET User Secrets

> [!IMPORTANT]
> **API Key Security Rule:** Never hardcode API keys in source code, commit them to git, or paste them into public repositories.

In local development, use [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) to store your Groq API key securely outside the source tree:

```bash
# Navigate to your consuming application directory
cd DevErrorAssistant.Demo

# Set your Groq API key in local User Secrets
dotnet user-secrets set "Groq:ApiKey" "gsk_your_actual_groq_api_key_here"
```

The library reads `Groq:ApiKey` automatically via standard ASP.NET Core `IConfiguration` provider hierarchy:
1. `options.Ai.ApiKey` (if set programmatically)
2. `builder.Configuration["Groq:ApiKey"]` (User Secrets in Development, Environment Variables, or Azure Key Vault in other environments).

---

## Privacy & Context Sanitization

Before sending any exception data to Groq, `DevErrorAssistant` passes the exception through an automated sanitization layer (`ExceptionSanitizer`):

- **Bearer Tokens**: `Authorization: Bearer <token>` is replaced with `Bearer [REDACTED_TOKEN]`.
- **Database Connection Strings**: Credentials such as `Password=...;`, `pwd=...;`, `User ID=...;` are replaced with `[REDACTED]`.
- **API Keys & Secrets**: Common patterns like `api_key: ...`, `client_secret: ...`, `gsk_...`, and `sk_...` are replaced with `[REDACTED]`.
- **Length Bounding**: Exception messages are truncated at `1000` characters and stack traces at `3000` characters (`... [truncated]`).
- **No Sensitive State**: HTTP request headers, cookies, query strings, request bodies, environment variables, and local source code files are **never** transmitted to the AI provider.

> [!WARNING]
> **Remaining Privacy Risk**: Automated regex scrubbing cannot guarantee detection of every possible custom proprietary secret, UUID, or sensitive business identifier embedded within exception messages. If your application handles sensitive personal data (PII/HIPAA/PCI), disable AI explanations in those environments or sanitize messages before throwing.

---

## How AI Disclaimers & Advice Work

- **Advisory Guidance Only**: AI explanations are suggestions, not guaranteed root causes. The error page includes an explicit disclaimer:
  > *"AI-generated suggestions are diagnostic guidance and may be incomplete or incorrect. Review all recommendations carefully before applying fixes."*
- **No Automatic Code Changes**: The package **never** modifies your source code, runs database migrations, or executes shell commands.
- **Safe Fallback**: If Groq is down, returns HTTP 429 (rate limited), or times out, the error page renders normal diagnostics with a non-sensitive notice (`AI explanation unavailable: ...`).

---

## Development vs. Production Behavior & Security

| Feature | Development Mode (`ASPNETCORE_ENVIRONMENT=Development`) | Production Mode (`ASPNETCORE_ENVIRONMENT=Production`) |
| :--- | :--- | :--- |
| **HTTP Status** | `500 Internal Server Error` | `500 Internal Server Error` |
| **Exception Type** | Full type name (e.g., `System.InvalidOperationException`) | Safe generic title (`"An error occurred"`) |
| **Exception Message** | Original developer error message | Safe generic message advising diagnostics are disabled |
| **Stack Trace** | Full interactive, collapsible `<details>` section | **Completely omitted** (null) |
| **AI Suggestions** | Active when configured | **Disabled by default** |
| **Server Logging** | Logged server-side via `ILogger.LogError` | Logged server-side via `ILogger.LogError` |

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

The test suite contains **30 automated tests** verifying:
1. Middleware catches unhandled request exceptions and returns HTTP 500.
2. Production mode suppresses stack traces and raw error messages.
3. Response-started protection: when `context.Response.HasStarted == true`, the middleware logs and rethrows without corrupting the stream.
4. AI-disabled mode makes zero provider requests.
5. Missing API key degrades gracefully without throwing.
6. Groq HTTP handling: 401 Unauthorized, 429 Rate Limited, 500 Server Error, invalid JSON, and timeouts degrade gracefully.
7. Context sanitization scrubs tokens, connection strings, and truncates oversized text.
8. AI output is safely HTML-encoded to prevent XSS script injection.
9. Cancellation tokens are propagated and handled cleanly.
10. Razor view is discovered from the RCL without copying `.cshtml` files.

---

## Testing the Demo Application

1. Start the demo:
   ```bash
   dotnet run --project DevErrorAssistant.Demo
   ```
2. Navigate to `http://localhost:5248`.
3. Click **"Trigger Test Exception"** or navigate to `/Home/ThrowError`.
4. Observe:
   - When no API key is configured: The page displays full exception details and notes that `Groq:ApiKey` is not configured (no crash!).
   - When an API key is set via User Secrets: The page displays AI-powered root-cause analysis, likely causes, recommended steps, and fixes.

---

## Creating and Consuming the NuGet Package Locally

### 1. Build the Package
```bash
dotnet pack -c Release DevErrorAssistant/DevErrorAssistant.csproj -o ./artifacts/packages
```

### 2. Verify Package Contents
```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::OpenRead("artifacts/packages/DevErrorAssistant.1.0.0.nupkg").Entries | Select-Object FullName
```

### 3. Add to a Consuming Project
```bash
dotnet nuget add source "D:\Dot net core projects\DevErrorAssistant\artifacts\packages" --name LocalDevPackages
dotnet add package DevErrorAssistant --version 1.0.0
```
