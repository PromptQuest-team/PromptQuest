using Google.GenAI;
using PromptQuest.Web.AI;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PromptQuest.Web.Configuration;
using PromptQuest.Web.Endpoints;
using PromptQuest.Web.Services;
using PromptQuest.Web.Services.Storage;
using PromptQuest.Web.Services.Storage.Db;
using PromptQuest.Web.Services.Storage.InMemory;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppOptions>(
    builder.Configuration.GetSection(AppOptions.SectionName));

if (builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("PromptQuest:UseInMemoryStorage"))
{
    builder.Services.AddSingleton<InMemoryPlayerStore>();
    builder.Services.AddSingleton<IPlayerStore>(sp => sp.GetRequiredService<InMemoryPlayerStore>());
    builder.Services.AddSingleton<IAttemptStore, InMemoryAttemptStore>();
    builder.Services.AddSingleton<ILeaderboardService, InMemoryLeaderboardService>();
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("Default");
    const string connectionError = "Connection string 'Default' must provide non-empty Host, Database and Username. "
        + "Configure ConnectionStrings__Default before starting the application.";

    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException(connectionError);

    NpgsqlConnectionStringBuilder connection;
    try
    {
        connection = new NpgsqlConnectionStringBuilder(connectionString);
    }
    catch (ArgumentException)
    {
        // Do not include the connection string or parser exception: they may contain credentials.
        throw new InvalidOperationException(connectionError);
    }

    if (string.IsNullOrWhiteSpace(connection.Host)
        || string.IsNullOrWhiteSpace(connection.Database)
        || string.IsNullOrWhiteSpace(connection.Username))
        throw new InvalidOperationException(connectionError);

    builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
    builder.Services.AddScoped<IPlayerStore, EfPlayerStore>();
    builder.Services.AddScoped<IAttemptStore, EfAttemptStore>();
    builder.Services.AddScoped<ILeaderboardService, EfLeaderboardService>();
}

builder.Services.AddSingleton<ILevelStore, JsonLevelStore>();
builder.Services.AddSingleton<ILeaderboardService, InMemoryLeaderboardService>();
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
