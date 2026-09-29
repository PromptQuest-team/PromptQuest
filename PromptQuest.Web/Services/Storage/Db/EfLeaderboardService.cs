using Microsoft.EntityFrameworkCore;
using PromptQuest.Web.Services.Storage.Db;
using PromptQuest.Web.Services.Storage;
using PromptQuest.Web.Dtos;

namespace PromptQuest.Web.Services;

public sealed class EfLeaderboardService : ILeaderboardService
{
    private readonly AppDbContext _db;

    public EfLeaderboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetForLevelAsync(
        string levelId, int take, CancellationToken ct = default)
    {
        if (take <= 0)
        {
            return Array.Empty<LeaderboardEntryDto>();
        }

        var rows = await _db.LevelProgresses.AsNoTracking()
            .Where(p => p.LevelId == levelId && p.Completed)
            .Join(_db.Players, p => p.PlayerId, pl => pl.Id, (p, pl) => new
            {
                pl.Nickname,
                Attempts = p.BestAttempts,
                TimeMs = p.BestTimeMs,
                Score = p.BestScore,
            })
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.TimeMs)
            .ThenBy(r => r.Nickname)
            .Take(take)
            .ToListAsync(ct);

        return rows
            .Select((r, i) => new LeaderboardEntryDto(i + 1, r.Nickname, r.Attempts, r.TimeMs, r.Score))
            .ToList();
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetGlobalAsync(
        int take, CancellationToken ct = default)
    {
        if (take <= 0)
        {
            return Array.Empty<LeaderboardEntryDto>();
        }

        var rows = await _db.Players.AsNoTracking()
            .Where(pl => pl.Progress.Any(p => p.Completed))
            .Select(pl => new
            {
                pl.Nickname,
                Attempts = pl.Progress.Where(p => p.Completed).Sum(p => p.BestAttempts),
                TimeMs = pl.Progress.Where(p => p.Completed).Sum(p => p.BestTimeMs),
                Score = pl.Progress.Where(p => p.Completed).Sum(p => p.BestScore),
            })
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.TimeMs)
            .ThenBy(r => r.Nickname)
            .Take(take)
            .ToListAsync(ct);

        return rows
            .Select((r, i) => new LeaderboardEntryDto(i + 1, r.Nickname, r.Attempts, r.TimeMs, r.Score))
            .ToList();
    }
}
