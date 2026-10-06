using System.Collections.Concurrent;
using System.Linq;
using PromptQuest.Web.Models;
using PromptQuest.Web.Services.Storage;

namespace PromptQuest.Web.Services.Storage.InMemory;

public sealed class InMemoryPlayerStore : IPlayerStore
{
    private readonly ConcurrentDictionary<Guid, Player> _players = new();

    public Task<Player?> GetAsync(Guid id, CancellationToken ct = default)
    {
        _players.TryGetValue(id, out var player);
        return Task.FromResult(player);
    }

    public Task<Player> CreateAsync(string nickname, CancellationToken ct = default)
    {
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Nickname = nickname,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _players[player.Id] = player;
        return Task.FromResult(player);
    }

    public Task<LevelProgress?> GetProgressAsync(Guid playerId, string levelId,
                                                  CancellationToken ct = default)
    {
        if (!_players.TryGetValue(playerId, out var player))
        {
            return Task.FromResult<LevelProgress?>(null);
        }

        foreach (var progress in player.Progress)
        {
            if (progress.LevelId == levelId)
            {
                return Task.FromResult<LevelProgress?>(progress);
            }
        }

        return Task.FromResult<LevelProgress?>(null);
    }

    public Task UpsertProgressAsync(LevelProgress progress, CancellationToken ct = default)
    {
        if (!_players.TryGetValue(progress.PlayerId, out var player))
        {
            return Task.CompletedTask;
        }

        LevelProgress? existing = null;
        foreach (var candidate in player.Progress)
        {
            if (candidate.Id == progress.Id)
            {
                existing = candidate;
                break;
            }
        }

        if (existing is null)
        {
            player.Progress.Add(progress);
        }
        else
        {
            existing.Completed = progress.Completed;
            existing.BestAttempts = progress.BestAttempts;
            existing.BestTimeMs = progress.BestTimeMs;
            existing.BestScore = progress.BestScore;
            existing.TotalAttempts = progress.TotalAttempts;
            existing.BestPromptLength = progress.BestPromptLength;
            existing.CompletedAt = progress.CompletedAt;
        }

        return Task.CompletedTask;
    }

    // Не часть контракта IPlayerStore: используется InMemoryLeaderboardService
    // для построения таблицы лидеров в Фазе 1. В Фазе 2 таблицу лидеров
    // строит SQL-запрос, а не перебор in-memory коллекции.
    public IReadOnlyCollection<Player> GetAllPlayers() => _players.Values.ToList();
}
