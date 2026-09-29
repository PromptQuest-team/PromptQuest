using Microsoft.EntityFrameworkCore;
using PromptQuest.Web.Services.Storage;
using PromptQuest.Web.Services.Storage.Db;
using PromptQuest.Web.Models;

namespace PromptQuest.Web.Services;

public sealed class EfPlayerStore : IPlayerStore
{
    private readonly AppDbContext _db;

    public EfPlayerStore(AppDbContext db)
    {
        _db = db;
    }

    public Task<Player?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return _db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    }

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

    public Task<LevelProgress?> GetProgressAsync(Guid playerId, string levelId,
                                                  CancellationToken ct = default)
    {
        return _db.LevelProgresses.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlayerId == playerId && p.LevelId == levelId, ct);
    }

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
