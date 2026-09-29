using Microsoft.Extensions.Options;
using PromptQuest.Web.Configuration;
using PromptQuest.Web.Dtos;
using PromptQuest.Web.Services.Storage;

namespace PromptQuest.Web.Endpoints;

public static class LevelEndpoints
{
    public static void MapLevelEndpoints(this WebApplication app)
    {
        app.MapGet("/api/levels", async (
            Guid? playerId,
            ILevelStore levelStore,
            IPlayerStore playerStore,
            IOptions<AppOptions> options,
            CancellationToken ct) =>
        {
            var levels = await levelStore.GetAllAsync(ct);
            var manualEntry = options.Value.ManualCodeEntry;

            var summaries = new List<LevelSummaryDto>();
            foreach (var level in levels)
            {
                var completed = false;
                var bestAttempts = 0;
                var bestTimeMs = 0;
                var bestScore = 0;

                if (playerId.HasValue)
                {
                    var progress = await playerStore.GetProgressAsync(playerId.Value, level.Id, ct);
                    if (progress is not null)
                    {
                        completed = progress.Completed;
                        bestAttempts = progress.BestAttempts;
                        bestTimeMs = progress.BestTimeMs;
                        bestScore = progress.BestScore;
                    }
                }

                summaries.Add(new LevelSummaryDto(
                    level.Id,
                    level.Order,
                    level.Category,
                    level.Title,
                    level.Difficulty,
                    completed,
                    bestAttempts,
                    bestTimeMs,
                    bestScore,
                    manualEntry));
            }

            return Results.Ok(summaries);
        });

        app.MapGet("/api/levels/{levelId}", async (
            string levelId,
            ILevelStore levelStore,
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

            var detail = new LevelDetailDto(
                level.Id,
                level.Order,
                level.Category,
                level.Title,
                level.Goal,
                level.Hint,
                level.Difficulty,
                level.InjectionMode,
                level.Scene,
                level.CodeTemplate,
                level.Validation,
                level.ForbiddenPatterns,
                options.Value.ManualCodeEntry);

            return Results.Ok(detail);
        });
    }
}
