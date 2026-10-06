using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PromptQuest.Web.Models;
using PromptQuest.Web.Services;
using PromptQuest.Web.Services.Storage;

namespace PromptQuest.Web.Tests;

// Заглушка ICodeGenerationService для интеграционных тестов — реальный Gemini
// API никогда не вызывается.
public sealed class FakeCodeGenerationService : ICodeGenerationService
{
    public Func<LevelDefinition, string, CodeGenerationResult> Handler { get; set; } =
        (_, _) => new CodeGenerationResult(true, "#pond{}", false, CodeSource.Ai, null);

    public Task<CodeGenerationResult> GenerateAsync(LevelDefinition level, string prompt, CancellationToken ct = default)
        => Task.FromResult(Handler(level, prompt));
}

public sealed class PromptQuestWebFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _configOverrides;

    public FakeCodeGenerationService CodeGen { get; } = new();

    public PromptQuestWebFactory(Dictionary<string, string?>? configOverrides = null)
    {
        // Program.cs reads ConnectionStrings:Default synchronously, before
        // WebApplicationFactory gets a chance to layer in test configuration
        // (see StorageRegistration.cs comment). An env var is visible at that
        // point because WebApplication.CreateBuilder loads env vars eagerly.
        // The value is never actually connected to: ConfigureServices below
        // replaces the EF-backed stores with in-memory ones before any
        // request reaches them.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", "Host=unused;Database=unused;Username=unused");

        _configOverrides = configOverrides ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": that would make Program.cs run Database.Migrate()
        // against the dummy connection string above.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(_configOverrides);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICodeGenerationService>();
            services.AddSingleton<ICodeGenerationService>(CodeGen);

            services.RemoveAll<IPlayerStore>();
            services.RemoveAll<IAttemptStore>();
            services.RemoveAll<ILeaderboardService>();
            services.AddStorage(useInMemoryStorage: true, connectionString: null);
        });
    }
}
