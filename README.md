
# DevErrorAssistant

[![NuGet Version](https://img.shields.io/nuget/v/DevErrorAssistant.svg)](https://www.nuget.org/packages/DevErrorAssistant)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-purple.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

> A developer-friendly ASP.NET Core diagnostic error page with optional AI-powered exception analysis.

`DevErrorAssistant` captures unhandled exceptions in your ASP.NET Core request pipeline and renders a diagnostic page. During development, you can inspect exception details and collapsible stack traces, with optional **Groq AI-powered explanations, likely causes, recommended steps, and possible fixes**. Detailed server information is hidden by default outside Development.

---

## ⚡ Quick Start

### 1. Install via NuGet

```bash
dotnet add package DevErrorAssistant
```

Or via Package Manager Console:

```powershell
Install-Package DevErrorAssistant
```

### 2. Configure `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDevErrorAssistant(options =>
{
    // AI explanations are optional and disabled by default.
    options.Ai.Enabled = builder.Environment.IsDevelopment();
});

var app = builder.Build();

// Register early to catch exceptions from downstream middleware.
app.UseDevErrorAssistant();

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

> **Why register early?** Placing `app.UseDevErrorAssistant()` before downstream middleware and endpoints allows it to catch unhandled exceptions from those components. Exceptions occurring after the HTTP response has started cannot be handled by replacing the response.

---

## 🚀 Key Features

- **Packaged Razor View (RCL):** Includes the diagnostic view in the NuGet package; no need to copy `.cshtml` files into your application.
- **Developer Diagnostics:** Displays exception information and a collapsible stack trace when detailed diagnostics are enabled.
- **Optional Groq AI Diagnostics:** Provides plain-English explanations, likely causes, troubleshooting steps, and possible fixes.
- **Graceful AI Fallback:** Handles common AI provider failures without requiring a successful AI response to render the error page.
- **Best-Effort Secret Sanitization:** Attempts to redact common credentials and tokens and limits exception-context length before AI analysis.
- **Production-Aware Diagnostics:** Detailed exception information is hidden by default outside Development.
- **Streaming Response Protection:** Checks `context.Response.HasStarted` and logs and rethrows exceptions when the response cannot safely be replaced.
- **HTML Output Encoding:** Encodes dynamic content in the HTML fallback renderer.
- **.NET Compatibility:** Targets .NET 8 and .NET 9.

---

## 🤖 Configuring AI Explanations (Groq)

AI diagnostics are **opt-in** and disabled by default. When enabled, `DevErrorAssistant` uses Groq's OpenAI-compatible Chat Completions API.

### Step 1: Set Your API Key Securely

For local development, use [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets):

```bash
dotnet user-secrets init
dotnet user-secrets set "Groq:ApiKey" "YOUR_GROQ_API_KEY"
```

Run `dotnet user-secrets init` only if User Secrets has not already been initialized for the project.

The library supports providing the API key through `options.Ai.ApiKey` or the `Groq:ApiKey` configuration setting. Never commit real API keys to source control.

### Step 2: Customize Options (Optional)

Configure AI enablement and the model in `Program.cs`. Other AI settings can be configured in `appsettings.Development.json`.

```csharp
builder.Services.AddDevErrorAssistant(options =>
{
    options.Ai.Enabled = builder.Environment.IsDevelopment();
    options.Ai.Model = "openai/gpt-oss-120b";
});
```

Configure the remaining AI settings in `appsettings.Development.json`:

```json
{
  "DevErrorAssistant": {
    "Ai": {
      "Provider": "Groq",
      "Timeout": "00:00:10",
      "Sanitization": {
        "MaxMessageLength": 1000,
        "MaxStackTraceLength": 3000,
        "RedactCommonSecrets": true
      }
    }
  }
}
```

Keep your API key in .NET User Secrets or another secure configuration source. Never commit a real API key to source control.

The configured model must be available to your Groq account. AI analysis depends on provider availability, network connectivity, and applicable API limits.

---

## 🔒 Privacy & Sanitization

Before sending exception context to the AI provider, `DevErrorAssistant` attempts to sanitize it.

| Data pattern | Sanitization behavior |
| :--- | :--- |
| Bearer and Basic authorization tokens | Attempts to replace detected tokens with a redaction marker |
| Connection-string credentials such as `Password`, `Pwd`, and `User ID` | Attempts to redact detected credential values |
| Common API-key and secret key/value patterns | Attempts to redact matching values |
| Oversized exception messages and stack traces | Truncates to configured maximum lengths |
| Nested inner exceptions | Includes a bounded summary, subject to sanitization settings |

**Important:** Sanitization is best-effort and cannot guarantee that every secret or piece of sensitive information will be removed.

When AI analysis is enabled, sanitized exception context may be sent to the external AI provider. The library does not intentionally send HTTP request bodies, cookies, session state, or entire configuration dumps as part of its exception-analysis context. However, exception messages and stack traces can themselves contain sensitive information.

Review the implementation and your provider's data-handling terms before enabling AI analysis in production.

---

## 🛡️ Development vs. Production Behavior

| Feature | Development (default behavior) | Other environments (default behavior) |
| :--- | :--- | :--- |
| HTTP status code | `500 Internal Server Error` | `500 Internal Server Error` |
| Exception details | Detailed diagnostics shown by default | Detailed diagnostics hidden by default |
| Stack trace | Available when detailed diagnostics are enabled | Hidden by default |
| AI analysis | Disabled unless explicitly enabled | Disabled unless explicitly enabled |
| Server-side logging | Exceptions logged through `ILogger` | Exceptions logged through `ILogger` |

The `ShowDetailedDiagnostics` option can override the default detail-visibility behavior. Review this setting carefully before exposing diagnostic information outside Development.

---

## 🧪 Testing with the Demo App

The repository includes a demo application in `DevErrorAssistant.Demo`.

Run it with:

```bash
dotnet run --project DevErrorAssistant.Demo/DevErrorAssistant.Demo.csproj
```

Open the local URL printed by the application, then trigger a sample exception using the demo's error-testing endpoint.

- **Without an API key:** AI analysis may be unavailable, but the diagnostic page should not depend on a successful AI response.
- **With an API key:** When AI is enabled and the provider request succeeds, the page can display an AI-generated explanation and recommendations.

---

## 📦 Building the NuGet Package Locally

Build the library package with:

```bash
dotnet pack DevErrorAssistant/DevErrorAssistant.csproj -c Release -o ./artifacts/packages
```

To test the package in another project, add the local package directory as a NuGet source:

```bash
dotnet nuget add source "./artifacts/packages" --name LocalDevPackages
```

Then install the package into your test application using that local source. Remove the source when you no longer need it:

```bash
dotnet nuget remove source LocalDevPackages
```

Before publishing, verify the generated `.nupkg`, package metadata, README inclusion, dependencies, and target frameworks.

---

## 📋 Running Automated Tests

Run the solution's test suite from the repository root:

```bash
dotnet test DevErrorAssistant.sln --configuration Release
```

Run the command and verify the actual results before adding a passing-test badge or publishing a test count.

---

## 📄 License

This project is intended to use the [MIT License](LICENSE). Ensure that the repository contains the matching `LICENSE` file before publishing.
