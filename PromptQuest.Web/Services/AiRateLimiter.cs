using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using PromptQuest.Web.Configuration;

namespace PromptQuest.Web.Services;

// Лимит запросов к ИИ на игрока (встроенный в .NET System.Threading.RateLimiting,
// без дополнительных пакетов). Партиция — playerId из тела запроса, поэтому лимит
// применяется программно внутри обработчика POST /api/attempts, а не через
// middleware ASP.NET Core RateLimiting: partition-ключ появляется только после
// привязки тела запроса, а не на этапе выбора партиции в конвейере middleware.
public sealed class AiRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<Guid> _limiter;

    public AiRateLimiter(IOptions<AppOptions> options)
    {
        var permitLimit = options.Value.AiRequestsPerMinutePerPlayer;

        _limiter = PartitionedRateLimiter.Create<Guid, Guid>(playerId =>
            RateLimitPartition.GetFixedWindowLimiter(playerId, _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = permitLimit,
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    }

    public RateLimitLease Acquire(Guid playerId) => _limiter.AttemptAcquire(playerId);

    public void Dispose() => _limiter.Dispose();
}
