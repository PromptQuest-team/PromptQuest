using Google.GenAI;
using PromptQuest.Web.AI;
using PromptQuest.Web.Configuration;
using PromptQuest.Web.Endpoints;
using PromptQuest.Web.Services;
using PromptQuest.Web.Services.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppOptions>(
    builder.Configuration.GetSection(AppOptions.SectionName));

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
