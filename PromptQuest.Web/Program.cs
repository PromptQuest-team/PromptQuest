using Microsoft.EntityFrameworkCore;
using PromptQuest.Web.Configuration;
using PromptQuest.Web.Endpoints;
using PromptQuest.Web.Services;
using PromptQuest.Web.Services.Storage;
using PromptQuest.Web.Services.Storage.Db;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppOptions>(
    builder.Configuration.GetSection(AppOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));

builder.Services.AddScoped<IPlayerStore, EfPlayerStore>();
builder.Services.AddScoped<IAttemptStore, EfAttemptStore>();
builder.Services.AddScoped<ILeaderboardService, EfLeaderboardService>();
builder.Services.AddSingleton<ILevelStore, JsonLevelStore>();
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
