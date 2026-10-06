using Microsoft.Extensions.Options;
using PromptQuest.Web.Configuration;
using PromptQuest.Web.Services;
using PromptQuest.Web.Services.Storage;

namespace PromptQuest.Web.Endpoints;

public static class LeaderboardEndpoints
{
    public static void MapLeaderboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/leaderboard/levels/{levelId}", async (
            string levelId,
            int? take,
            ILevelStore levelStore,
            ILeaderboardService leaderboardService,
            IOptions<AppOptions> options,
            CancellationToken ct) =>
        {
            var level = await levelStore.GetAsync(levelId, ct);
            if (level is null)
            {
                return Results.Problem(
                    detail: "Уровень не найден.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            var entries = await leaderboardService.GetForLevelAsync(
                levelId, take ?? options.Value.LeaderboardTake, ct);

            return Results.Ok(entries);
        });
    }
}
