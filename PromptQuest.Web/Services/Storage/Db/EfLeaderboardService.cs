using Microsoft.EntityFrameworkCore;
using PromptQuest.Web.Services.Storage.Db;
using PromptQuest.Web.Services.Storage;
using PromptQuest.Web.Dtos;

namespace PromptQuest.Web.Services;

public sealed class EfLeaderboardService : ILeaderboardService
{
    private readonly AppDbContext _db;

    /// <summary>Initializes the leaderboard service using the supplied database context.</summary>
    public EfLeaderboardService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Ranks players who completed the level using their stored best results.</summary>
    /// <param name="levelId">The level whose completed progress is ranked.</param>
    /// <param name="take">Maximum entries; zero or negative values return an empty list without querying the database.</param>
    /// <param name="ct">Cancels the database query when one is needed.</param>
    /// <returns>
    /// Entries with sequential ranks starting at 1, ordered by score descending, time in milliseconds
    /// ascending, then nickname ascending. Returns an empty list when no completed progress matches.
    /// </returns>
    /// <remarks>Database and cancellation errors from the query propagate to the caller.</remarks>
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

    /// <summary>Ranks players with completed levels by summing their best attempts, times, and scores across those levels.</summary>
    /// <param name="take">Maximum entries; zero or negative values return an empty list without querying the database.</param>
    /// <param name="ct">Cancels the database query when one is needed.</param>
    /// <returns>
    /// Entries with sequential ranks starting at 1, ordered by total score descending, total time in
    /// milliseconds ascending, then nickname ascending. Returns an empty list when no player has completed a level.
    /// </returns>
    /// <remarks>Database and cancellation errors from the query propagate to the caller.</remarks>
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
