using System.Text.Json;
using Microsoft.Extensions.Options;
using PromptQuest.Web.Configuration;
using PromptQuest.Web.Models;

namespace PromptQuest.Web.Services;

public sealed class JsonLevelStore : ILevelStore
{
    private readonly IReadOnlyList<LevelDefinition> _levels;
    private readonly Dictionary<string, LevelDefinition> _byId;

    public JsonLevelStore(IOptions<AppOptions> options, IHostEnvironment environment)
    {
        var path = Path.Combine(environment.ContentRootPath, options.Value.LevelsFilePath);
        var json = File.ReadAllText(path);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        var levels = JsonSerializer.Deserialize<List<LevelDefinition>>(json, jsonOptions)
                     ?? new List<LevelDefinition>();

        levels.Sort((a, b) => a.Order.CompareTo(b.Order));

        _levels = levels;
        _byId = new Dictionary<string, LevelDefinition>();
        foreach (var level in levels)
        {
            _byId[level.Id] = level;
        }
    }

    public Task<IReadOnlyList<LevelDefinition>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_levels);
    }

    public Task<LevelDefinition?> GetAsync(string levelId, CancellationToken ct = default)
    {
        _byId.TryGetValue(levelId, out var level);
        return Task.FromResult(level);
    }
}
