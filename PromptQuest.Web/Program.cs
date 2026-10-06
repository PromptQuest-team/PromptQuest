using Google.GenAI;
using Microsoft.EntityFrameworkCore;
using PromptQuest.Web.AI;
using PromptQuest.Web.Configuration;
using PromptQuest.Web.Endpoints;
using PromptQuest.Web.Services;
using PromptQuest.Web.Services.Storage;
using PromptQuest.Web.Services.Storage.Db;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppOptions>(
    builder.Configuration.GetSection(AppOptions.SectionName));

// In-memory storage is only ever wired up directly by tests
// (PromptQuestWebFactory/StorageRegistration unit tests) — the running
// application always talks to the real PostgreSQL database, in every
// environment, so Development behaves exactly like production.
builder.Services.AddStorage(useInMemoryStorage: false, builder.Configuration.GetConnectionString("Default"));

builder.Services.AddSingleton<ILevelStore, JsonLevelStore>();
builder.Services.AddSingleton<ICodeGenerationService, AiAgent>();
builder.Services.AddSingleton<AiRateLimiter>();

builder.Services.AddProblemDetails();

builder.Services.AddSingleton(sp =>
{
    var apiKey = builder.Configuration["Gemini:ApiKey"];
    return new Client(apiKey: apiKey);
});

var app = builder.Build();

// Development has no separate deployment step to run migrations, so the
// schema is created/updated automatically on startup. Production/Staging
// are expected to apply migrations as part of their own deployment process.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPlayerEndpoints();
app.MapLevelEndpoints();
app.MapAttemptEndpoints();
app.MapLeaderboardEndpoints();

app.Run();

// Делает неявный класс Program из top-level statements доступным для
// WebApplicationFactory<Program> в тестовом проекте. Поведения не меняет.
public partial class Program { }
