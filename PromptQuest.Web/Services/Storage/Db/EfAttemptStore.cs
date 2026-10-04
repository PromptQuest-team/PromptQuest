using Microsoft.EntityFrameworkCore;
using PromptQuest.Web.Services.Storage.Db;
using PromptQuest.Web.Services.Storage;
using PromptQuest.Web.Models;

namespace PromptQuest.Web.Services;

public sealed class EfAttemptStore : IAttemptStore
{
    private readonly AppDbContext _db;

    /// <summary>Initializes the attempt store using the supplied database context.</summary>
    public EfAttemptStore(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Retrieves an attempt without tracking changes to it.</summary>
    /// <returns>The matching attempt, or <see langword="null"/> if it does not exist.</returns>
    /// <remarks>Database and cancellation errors propagate to the caller.</remarks>
    public Task<Attempt?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return _db.Attempts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    /// <summary>Inserts the supplied attempt and saves pending changes in the context.</summary>
    /// <remarks>Database and cancellation errors propagate to the caller.</remarks>
    public async Task AddAsync(Attempt attempt, CancellationToken ct = default)
    {
        _db.Attempts.Add(attempt);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Copies the supplied attempt's scalar values to the record with the same ID and saves
    /// pending changes in the context. Returns without saving if the record does not exist.
    /// </summary>
    /// <remarks>Database and cancellation errors propagate to the caller.</remarks>
    public async Task UpdateAsync(Attempt attempt, CancellationToken ct = default)
    {
        var tracked = await _db.Attempts.FirstOrDefaultAsync(a => a.Id == attempt.Id, ct);
        if (tracked is null)
        {
            return;
        }

        if (!ReferenceEquals(tracked, attempt))
        {
            _db.Entry(tracked).CurrentValues.SetValues(attempt);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Counts all stored attempts for the player and level, including those without results.</summary>
    /// <remarks>Database and cancellation errors propagate to the caller.</remarks>
    public Task<int> CountAsync(Guid playerId, string levelId, CancellationToken ct = default)
    {
        return _db.Attempts.CountAsync(a => a.PlayerId == playerId && a.LevelId == levelId, ct);
    }
}
