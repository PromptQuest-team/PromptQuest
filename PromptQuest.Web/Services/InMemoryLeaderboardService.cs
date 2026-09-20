using PromptQuest.Web.Dtos;
using PromptQuest.Web.Models;

namespace PromptQuest.Web.Services;

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
        var rows = new List<(string Nickname, int Attempts, int TimeMs, int Score)>();

        foreach (var player in _playerStore.GetAllPlayers())
        {
            foreach (var progress in player.Progress)
            {
                if (progress.LevelId == levelId && progress.Completed)
                {
                    rows.Add((player.Nickname, progress.BestAttempts, progress.BestTimeMs, progress.BestScore));
                }
            }
        }

        rows.Sort((a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : a.TimeMs.CompareTo(b.TimeMs);
        });

        var result = BuildEntries(rows, take);
        return Task.FromResult<IReadOnlyList<LeaderboardEntryDto>>(result);
    }

    public Task<IReadOnlyList<LeaderboardEntryDto>> GetGlobalAsync(
        int take, CancellationToken ct = default)
    {
        var rows = new List<(string Nickname, int Attempts, int TimeMs, int Score)>();

        foreach (var player in _playerStore.GetAllPlayers())
        {
            var totalScore = 0;
            var totalAttempts = 0;
            var totalTimeMs = 0;
            var hasCompleted = false;

            foreach (var progress in player.Progress)
            {
                if (progress.Completed)
                {
                    hasCompleted = true;
                    totalScore += progress.BestScore;
                    totalAttempts += progress.BestAttempts;
                    totalTimeMs += progress.BestTimeMs;
                }
            }

            if (hasCompleted)
            {
                rows.Add((player.Nickname, totalAttempts, totalTimeMs, totalScore));
            }
        }

        rows.Sort((a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            return byScore != 0 ? byScore : a.TimeMs.CompareTo(b.TimeMs);
        });

        var result = BuildEntries(rows, take);
        return Task.FromResult<IReadOnlyList<LeaderboardEntryDto>>(result);
    }

    private static List<LeaderboardEntryDto> BuildEntries(
        List<(string Nickname, int Attempts, int TimeMs, int Score)> rows, int take)
    {
        var entries = new List<LeaderboardEntryDto>();
        var limit = Math.Min(take, rows.Count);

        for (var i = 0; i < limit; i++)
        {
            var row = rows[i];
            entries.Add(new LeaderboardEntryDto(i + 1, row.Nickname, row.Attempts, row.TimeMs, row.Score));
        }

        return entries;
    }
}
