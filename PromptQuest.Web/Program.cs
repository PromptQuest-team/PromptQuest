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

// Temporary: Development defaults to in-memory storage (no shared dev DB
// exists yet). Every other environment, and Development with
// PromptQuest:UseInMemoryStorage explicitly set to false, always requires
// PostgreSQL via ConnectionStrings:Default.
var useInMemoryStorage = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("PromptQuest:UseInMemoryStorage");

builder.Services.AddStorage(useInMemoryStorage, builder.Configuration.GetConnectionString("Default"));

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
// schema is created/updated automatically on startup - but only when a real
// database is actually in use; AppDbContext isn't registered at all in the
// in-memory branch, so this must stay conditional on useInMemoryStorage, not
// just on the environment. Production/Staging are expected to apply
// migrations as part of their own deployment process.
if (app.Environment.IsDevelopment() && !useInMemoryStorage)
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
