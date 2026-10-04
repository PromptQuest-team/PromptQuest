using Microsoft.EntityFrameworkCore;
using PromptQuest.Web.Services.Storage;
using PromptQuest.Web.Services.Storage.Db;
using PromptQuest.Web.Models;

namespace PromptQuest.Web.Services;

public sealed class EfPlayerStore : IPlayerStore
{
    private readonly AppDbContext _db;

    /// <summary>Initializes the player store using the supplied database context.</summary>
    public EfPlayerStore(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Retrieves a player without tracking changes or loading level progress.</summary>
    /// <returns>The matching player, or <see langword="null"/> if it does not exist.</returns>
    /// <remarks>Database and cancellation errors propagate to the caller.</remarks>
    public Task<Player?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    /// <summary>Creates a player and saves pending changes in the context.</summary>
    /// <param name="nickname">The nickname to store as supplied, without trimming or application-level validation.</param>
    /// <param name="ct">Cancels the database save.</param>
    /// <returns>The tracked player with a new ID and the current UTC creation time.</returns>
    /// <remarks>Database and cancellation errors propagate to the caller.</remarks>
    public async Task<Player> CreateAsync(string nickname, CancellationToken ct = default)
    {
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Nickname = nickname,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.Players.Add(player);
        await _db.SaveChangesAsync(ct);
        return player;
    }

    /// <summary>Retrieves the player's progress for a level without tracking changes to it.</summary>
    /// <returns>The matching progress, or <see langword="null"/> if no record exists.</returns>
    /// <remarks>Database and cancellation errors propagate to the caller.</remarks>
    public Task<LevelProgress?> GetProgressAsync(Guid playerId, string levelId,
                                                  CancellationToken ct = default)
    {
        return _db.LevelProgresses.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlayerId == playerId && p.LevelId == levelId, ct);
    }

    /// <summary>
    /// Inserts progress or overwrites completion and attempt statistics on the first record
    /// matching its ID or player and level, preserving that record's identity and ownership.
    /// </summary>
    /// <remarks>
    /// Returns without saving if the supplied player does not exist. Otherwise, saves all pending
    /// changes in the context; best values are copied without comparing scores.
    /// Database and cancellation errors propagate to the caller.
    /// </remarks>
    public async Task UpsertProgressAsync(LevelProgress progress, CancellationToken ct = default)
    {
        var playerExists = await _db.Players.AnyAsync(p => p.Id == progress.PlayerId, ct);
        if (!playerExists)
        {
            return;
        }

        var existing = await _db.LevelProgresses.FirstOrDefaultAsync(
            p => p.Id == progress.Id ||
                 (p.PlayerId == progress.PlayerId && p.LevelId == progress.LevelId), ct);

        if (existing is null)
        {
            _db.LevelProgresses.Add(progress);
        }
        else if (!ReferenceEquals(existing, progress))
        {
            existing.Completed = progress.Completed;
            existing.BestAttempts = progress.BestAttempts;
            existing.BestTimeMs = progress.BestTimeMs;
            existing.BestScore = progress.BestScore;
            existing.TotalAttempts = progress.TotalAttempts;
            existing.CompletedAt = progress.CompletedAt;
        }

        await _db.SaveChangesAsync(ct);
    }
}
