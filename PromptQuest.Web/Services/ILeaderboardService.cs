using PromptQuest.Web.Dtos;

namespace PromptQuest.Web.Services;

public interface ILeaderboardService
{
    Task<IReadOnlyList<LeaderboardEntryDto>> GetForLevelAsync(
        string levelId, int take, CancellationToken ct = default);
    Task<IReadOnlyList<LeaderboardEntryDto>> GetGlobalAsync(
        int take, CancellationToken ct = default);
}
