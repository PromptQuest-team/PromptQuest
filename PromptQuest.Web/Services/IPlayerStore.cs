using PromptQuest.Web.Models;

namespace PromptQuest.Web.Services;

public interface IPlayerStore
{
    Task<Player?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Player>  CreateAsync(string nickname, CancellationToken ct = default);
    Task<LevelProgress?> GetProgressAsync(Guid playerId, string levelId,
                                          CancellationToken ct = default);
    Task UpsertProgressAsync(LevelProgress progress, CancellationToken ct = default);
}
