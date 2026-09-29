using Microsoft.EntityFrameworkCore;
using PromptQuest.Web.Services.Storage.Db;
using PromptQuest.Web.Services.Storage;
using PromptQuest.Web.Models;

namespace PromptQuest.Web.Services;

public sealed class EfAttemptStore : IAttemptStore
{
    private readonly AppDbContext _db;

    public EfAttemptStore(AppDbContext db)
    {
        _db = db;
    }

    public Task<Attempt?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return _db.Attempts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task AddAsync(Attempt attempt, CancellationToken ct = default)
    {
        _db.Attempts.Add(attempt);
        await _db.SaveChangesAsync(ct);
    }

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

    public Task<int> CountAsync(Guid playerId, string levelId, CancellationToken ct = default)
    {
        return _db.Attempts.CountAsync(a => a.PlayerId == playerId && a.LevelId == levelId, ct);
    }
}
