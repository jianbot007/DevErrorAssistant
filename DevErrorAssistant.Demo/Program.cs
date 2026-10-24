var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register DevErrorAssistant services and configure AI options
builder.Services.AddDevErrorAssistant(options =>
{
    // Opt-in to AI explanations (enabled in development demo)
    options.Ai.Enabled = builder.Configuration.GetValue<bool>("DevErrorAssistant:Ai:Enabled", true);
    options.Ai.Model = builder.Configuration["DevErrorAssistant:Ai:Model"] ?? "llama-3.3-70b-versatile";
    // Note: The Groq API key is resolved automatically at runtime from configuration (Groq:ApiKey) or User Secrets.
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// Register DevErrorAssistant at the beginning of the pipeline so it wraps all downstream middleware
// (routing, authentication, authorization, static assets, and controller actions).
app.UseDevErrorAssistant();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
