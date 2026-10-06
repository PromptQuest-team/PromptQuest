using PromptQuest.Web.Dtos;
using PromptQuest.Web.Models;

namespace PromptQuest.Web.Services.Storage.InMemory;

public sealed class InMemoryLeaderboardService : ILeaderboardService
{
    private readonly InMemoryPlayerStore _playerStore;

    public InMemoryLeaderboardService(InMemoryPlayerStore playerStore)
    {
        _playerStore = playerStore;
    }

    public Task<IReadOnlyList<LeaderboardEntryDto>> GetForLevelAsync(
        string levelId, int take, CancellationToken ct = default)
    {
        var rows = new List<(string Nickname, int PromptLength, DateTimeOffset CompletedAt)>();

        foreach (var player in _playerStore.GetAllPlayers())
        {
            foreach (var progress in player.Progress)
            {
                if (progress.LevelId == levelId && progress.Completed)
                {
                    rows.Add((player.Nickname, progress.BestPromptLength, progress.CompletedAt ?? DateTimeOffset.MaxValue));
                }
            }
        }

        // По возрастанию длины промта; при равенстве — кто достиг раньше.
        rows.Sort((a, b) =>
        {
            var byLength = a.PromptLength.CompareTo(b.PromptLength);
            return byLength != 0 ? byLength : a.CompletedAt.CompareTo(b.CompletedAt);
        });

        var entries = new List<LeaderboardEntryDto>();
        var limit = Math.Min(take, rows.Count);
        for (var i = 0; i < limit; i++)
        {
            var row = rows[i];
            entries.Add(new LeaderboardEntryDto(i + 1, row.Nickname, row.PromptLength));
        }

        return Task.FromResult<IReadOnlyList<LeaderboardEntryDto>>(entries);
    }
}
