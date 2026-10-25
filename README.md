# DevErrorAssistant

[![NuGet Version](https://img.shields.io/nuget/v/DevErrorAssistant.svg)](https://www.nuget.org/packages/DevErrorAssistant)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-purple.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/Tests-30%20Passing-brightgreen.svg)]()

> A developer-friendly ASP.NET Core diagnostic error page with optional, privacy-conscious AI-powered exception analysis.

`DevErrorAssistant` captures unhandled exceptions in your ASP.NET Core request pipeline and renders a clean, interactive diagnostic page. During development, you get complete error diagnostics, collapsible stack traces, and optional **Groq AI-powered explanations, likely causes, and code fixes**. In production, sensitive server details are safely suppressed by default.

---

## ⚡ Quick Start (30 Seconds)

### 1. Install via NuGet

```bash
dotnet add package DevErrorAssistant
```

Or via Package Manager Console:
```powershell
Install-Package DevErrorAssistant
```

---

### 2. Configure `Program.cs`

Add **two lines** to your `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// 1. Register DevErrorAssistant services
builder.Services.AddDevErrorAssistant(options =>
{
    // Opt-in to AI explanations in Development mode
    options.Ai.Enabled = builder.Environment.IsDevelopment();
});

var app = builder.Build();

// 2. Register DevErrorAssistant as the FIRST middleware in the pipeline
app.UseDevErrorAssistant();

// Standard downstream middleware
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

> **Why register first?** Placing `app.UseDevErrorAssistant()` at the top ensures its `try/catch` boundary encompasses all downstream routing, authentication, authorization, and endpoint execution.

---

## 🚀 Key Features

- **Packaged Razor View (RCL)**: Ships precompiled views directly inside the NuGet package. No need to copy `.cshtml` files into your project.
- **Interactive Developer Diagnostics**: Clean, modern dark-mode interface displaying exception type, message, and collapsible stack trace.
- **Optional Groq AI Diagnostics**: Translates cryptic runtime exceptions into plain-English explanations, likely root causes, step-by-step troubleshooting, and concrete code fixes.
- **Zero Crashes & Fault Tolerant**: If AI is disabled, network drops, or no API key is configured, your application never crashes. The diagnostic page renders normally with a status notice.
- **Automatic Privacy Sanitization**: Scrubs database passwords, Bearer tokens, and API keys, and bounds message/trace lengths before sending context to external AI.
- **Safe-by-Default Production Mode**: In Staging/Production, detailed stack traces and raw error messages are automatically hidden to protect sensitive server paths and credentials.
- **Streaming Response Protection**: Checks `context.Response.HasStarted`. If headers have already streamed, the middleware logs and safely rethrows without corrupting the response.
- **XSS-Safe**: All dynamic exception data and AI responses are safely HTML-encoded.
- **Broad Compatibility**: Built for both **.NET 8 (LTS)** and **.NET 9**.

---

## 🤖 Configuring AI Explanations (Groq)

AI diagnostics are completely **opt-in** and disabled by default. When enabled, `DevErrorAssistant` uses Groq's official OpenAI-compatible Chat Completions API.

### Step 1: Set Your API Key Securely

For local development, store your Groq API key in [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) so it is never committed to Git:

```bash
dotnet user-secrets set "Groq:ApiKey" "gsk_your_groq_api_key_here"
```

> **Configuration Precedence:** The library automatically resolves the key at runtime from standard .NET configuration providers:
> 1. `options.Ai.ApiKey` (if set programmatically)
> 2. `builder.Configuration["Groq:ApiKey"]` (User Secrets, Environment Variables, or Azure Key Vault)

### Step 2: Customizing Options (Optional)

You can customize the model, timeout, and sanitization settings in `Program.cs` or `appsettings.json`:

```csharp
builder.Services.AddDevErrorAssistant(options =>
{
    // Enable/disable AI
    options.Ai.Enabled = builder.Environment.IsDevelopment();

    // Groq model (default: "llama-3.3-70b-versatile")
    // Also supports "openai/gpt-oss-120b", "openai/gpt-oss-20b", etc.
    options.Ai.Model = "llama-3.3-70b-versatile";

    // Request timeout (default: 10 seconds)
    options.Ai.Timeout = TimeSpan.FromSeconds(10);

    // Context sanitization limits
    options.Ai.Sanitization.MaxMessageLength = 1000;
    options.Ai.Sanitization.MaxStackTraceLength = 3000;
    options.Ai.Sanitization.RedactCommonSecrets = true;
});
```

Or configure via `appsettings.Development.json`:

```json
{
  "DevErrorAssistant": {
    "Ai": {
      "Enabled": true,
      "Model": "llama-3.3-70b-versatile"
    }
  }
}
```

---

## 🔒 Privacy & Sanitization

Before sending exception context to the AI provider, `DevErrorAssistant` applies strict sanitization:

| Scrubbed Data | Replacement |
| :--- | :--- |
| `Authorization: Bearer <token>` | `Bearer [REDACTED_TOKEN]` |
| `Password=...;`, `pwd=...;`, `User ID=...;` | `[REDACTED]` |
| API keys matching `gsk_*`, `sk_*`, `api_key: ...` | `[REDACTED]` |
| Oversized messages / stack traces | Truncated to safe bounded lengths |

> **Privacy Guarantee:** HTTP request bodies, cookies, session state, authorization headers, environment variables, configuration dumps, and local source code files are **never** transmitted to the AI provider.

---

## 🛡️ Development vs. Production Behavior

| Feature | Development Mode (`ASPNETCORE_ENVIRONMENT=Development`) | Production Mode (`ASPNETCORE_ENVIRONMENT=Production`) |
| :--- | :--- | :--- |
| **HTTP Status Code** | `500 Internal Server Error` | `500 Internal Server Error` |
| **Exception Type** | Full type (e.g., `System.NullReferenceException`) | Generic safe title (`"An error occurred"`) |
| **Exception Message** | Raw developer message | Safe generic message |
| **Stack Trace** | Interactive collapsible `<details>` | **Completely omitted** (null) |
| **AI Suggestions** | Active (if enabled and key configured) | **Disabled** |
| **Server Logs** | Full stack trace logged via `ILogger.LogError` | Full stack trace logged via `ILogger.LogError` |

---

## 🧪 Testing with the Demo App

A complete demo application is included in `DevErrorAssistant.Demo`:

1. Run the demo:
   ```bash
   dotnet run --project DevErrorAssistant.Demo
   ```
2. Open `http://localhost:5248` in your browser.
3. Click **"Trigger Test Exception"** (`/Home/ThrowError`).
4. **Without an API key**: The error page displays the full exception, stack trace, and clearly notes that `Groq:ApiKey` is not configured (no crash!).
5. **With an API key set in User Secrets**: The page displays AI-generated explanation, likely causes, recommended steps, and fixes.

---

## 📦 Building the NuGet Package Locally

To build the NuGet package and symbols:

```bash
dotnet pack -c Release DevErrorAssistant/DevErrorAssistant.csproj -o ./artifacts/packages
```

To use the local package in another project:
```bash
dotnet nuget add source "<repo-path>/artifacts/packages" --name LocalDevPackages
dotnet add package DevErrorAssistant --version 1.0.0
```

---

## 📋 Running Automated Tests

The solution includes a comprehensive test suite covering middleware pipeline execution, production security, AI HTTP mock flows, network timeouts, and sanitization:

```bash
dotnet test
```

```text
Passed!  - Failed: 0, Passed: 30, Skipped: 0, Total: 30
```

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
