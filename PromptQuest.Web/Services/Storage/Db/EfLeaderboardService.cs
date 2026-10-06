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

    /// <summary>Ranks players who completed the level by their recorded best prompt length.</summary>
    /// <param name="levelId">The level whose completed progress is ranked.</param>
    /// <param name="take">Maximum entries; zero or negative values return an empty list without querying the database.</param>
    /// <param name="ct">Cancels the database query when one is needed.</param>
    /// <returns>
    /// Entries with sequential ranks starting at 1, ordered by prompt length ascending, then by
    /// completion time ascending (earlier wins a tie). Returns an empty list when no completed
    /// progress matches.
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
                PromptLength = p.BestPromptLength,
                CompletedAt = p.CompletedAt ?? DateTimeOffset.MaxValue,
            })
            .OrderBy(r => r.PromptLength)
            .ThenBy(r => r.CompletedAt)
            .Take(take)
            .ToListAsync(ct);

        return rows
            .Select((r, i) => new LeaderboardEntryDto(i + 1, r.Nickname, r.PromptLength))
            .ToList();
    }
}
