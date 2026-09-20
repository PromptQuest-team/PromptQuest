using PromptQuest.Web.Configuration;
using PromptQuest.Web.Endpoints;
using PromptQuest.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppOptions>(
    builder.Configuration.GetSection(AppOptions.SectionName));

builder.Services.AddSingleton<InMemoryPlayerStore>();
builder.Services.AddSingleton<IPlayerStore>(sp => sp.GetRequiredService<InMemoryPlayerStore>());
builder.Services.AddSingleton<IAttemptStore, InMemoryAttemptStore>();
builder.Services.AddSingleton<ILevelStore, JsonLevelStore>();
builder.Services.AddSingleton<ILeaderboardService, InMemoryLeaderboardService>();
builder.Services.AddSingleton<ICodeGenerationService, ManualCodeGenerationService>();

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPlayerEndpoints();
app.MapLevelEndpoints();
app.MapAttemptEndpoints();
app.MapLeaderboardEndpoints();

app.Run();
