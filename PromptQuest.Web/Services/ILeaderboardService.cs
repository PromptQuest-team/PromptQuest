using PromptQuest.Web.Dtos;

namespace PromptQuest.Web.Services;

// Общего зачёта нет: время и число попыток больше не участвуют в рейтинге, а
// суммировать длины промтов по разным уровням не имеет смысла как метрика.
public interface ILeaderboardService
{
    Task<IReadOnlyList<LeaderboardEntryDto>> GetForLevelAsync(
        string levelId, int take, CancellationToken ct = default);
}
