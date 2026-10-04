using PromptQuest.Web.Models;

namespace PromptQuest.Web.Services.Storage;

public interface IAttemptStore
{
    Task<Attempt?> GetAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Attempt attempt, CancellationToken ct = default);
    Task UpdateAsync(Attempt attempt, CancellationToken ct = default);
    Task<int> CountAsync(Guid playerId, string levelId, CancellationToken ct = default);
}
