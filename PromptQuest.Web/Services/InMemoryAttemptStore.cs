using System.Collections.Concurrent;
using PromptQuest.Web.Models;

namespace PromptQuest.Web.Services;

public sealed class InMemoryAttemptStore : IAttemptStore
{
    private readonly ConcurrentDictionary<Guid, Attempt> _attempts = new();

    public Task<Attempt?> GetAsync(Guid id, CancellationToken ct = default)
    {
        _attempts.TryGetValue(id, out var attempt);
        return Task.FromResult(attempt);
    }

    public Task AddAsync(Attempt attempt, CancellationToken ct = default)
    {
        _attempts[attempt.Id] = attempt;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Attempt attempt, CancellationToken ct = default)
    {
        _attempts[attempt.Id] = attempt;
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(Guid playerId, string levelId, CancellationToken ct = default)
    {
        var count = 0;
        foreach (var attempt in _attempts.Values)
        {
            if (attempt.PlayerId == playerId && attempt.LevelId == levelId)
            {
                count++;
            }
        }

        return Task.FromResult(count);
    }
}
