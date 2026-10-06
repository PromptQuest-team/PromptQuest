using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PromptQuest.Web.Models;
using PromptQuest.Web.Services;

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
        _configOverrides = configOverrides ?? new Dictionary<string, string?>
        {
            ["PromptQuest:UseInMemoryStorage"] = "true",
        };
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(_configOverrides);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICodeGenerationService>();
            services.AddSingleton<ICodeGenerationService>(CodeGen);
        });
    }
}
