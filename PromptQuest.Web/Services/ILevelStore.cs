using PromptQuest.Web.Models;

namespace PromptQuest.Web.Services;

public interface ILevelStore
{
    Task<IReadOnlyList<LevelDefinition>> GetAllAsync(CancellationToken ct = default);
    Task<LevelDefinition?> GetAsync(string levelId, CancellationToken ct = default);
}
