using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using PromptQuest.Web.Configuration;
using PromptQuest.Web.Dtos;
using PromptQuest.Web.Models;
using PromptQuest.Web.Services;
using PromptQuest.Web.Services.Storage;

namespace PromptQuest.Web.Endpoints;

public static class AttemptEndpoints
{
    public static void MapAttemptEndpoints(this WebApplication app)
    {
        app.MapPost("/api/attempts", async (
            CreateAttemptRequest request,
            HttpContext httpContext,
            IPlayerStore playerStore,
            ILevelStore levelStore,
            IAttemptStore attemptStore,
            ICodeGenerationService codeGenerationService,
            AiRateLimiter rateLimiter,
            IOptions<AppOptions> options,
            CancellationToken ct) =>
        {
            var player = await playerStore.GetAsync(request.PlayerId, ct);
            if (player is null)
            {
                return Results.Problem(
                    detail: "Игрок не найден.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            var level = await levelStore.GetAsync(request.LevelId, ct);
            if (level is null)
            {
                return Results.Problem(
                    detail: "Уровень не найден.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            var prompt = request.Prompt ?? "";
            if (prompt.Length > options.Value.MaxPromptLength)
            {
                return Results.Problem(
                    detail: $"Промт длиннее {options.Value.MaxPromptLength} символов.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            using var lease = rateLimiter.Acquire(request.PlayerId);
            if (!lease.IsAcquired)
            {
                var retryAfterSeconds = 60;
                if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                }

                httpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();

                return Results.Problem(
                    detail: $"Слишком много запросов к ИИ. Подождите {retryAfterSeconds} секунд.",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var generation = await codeGenerationService.GenerateAsync(level, prompt, ct);
            if (!generation.Success)
            {
                return Results.Problem(
                    detail: generation.Error ?? "ИИ не ответил.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            var attemptNumber = await attemptStore.CountAsync(request.PlayerId, request.LevelId, ct) + 1;

            var attempt = new Attempt
            {
                Id = Guid.NewGuid(),
                PlayerId = request.PlayerId,
                LevelId = request.LevelId,
                AttemptNumber = attemptNumber,
                Prompt = prompt,
                Code = generation.Code,
                Source = generation.Source,
                Passed = null,
                ElapsedMs = 0,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await attemptStore.AddAsync(attempt, ct);

            var manualEntry = options.Value.ManualCodeEntry;
            var sourceText = manualEntry ? "manual" : "ai";

            var response = new CreateAttemptResponse(
                attempt.Id,
                attempt.AttemptNumber,
                attempt.Code,
                sourceText,
                manualEntry);

            return Results.Ok(response);
        });

        app.MapPost("/api/attempts/{attemptId:guid}/result", async (
            Guid attemptId,
            SubmitResultRequest request,
            IAttemptStore attemptStore,
            IPlayerStore playerStore,
            ILevelStore levelStore,
            IOptions<AppOptions> options,
            CancellationToken ct) =>
        {
            var attempt = await attemptStore.GetAsync(attemptId, ct);
            if (attempt is null)
            {
                return Results.Problem(
                    detail: "Попытка не найдена.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            var code = request.Code ?? "";
            if (code.Length > options.Value.MaxCodeLength)
            {
                return Results.Problem(
                    detail: $"Код длиннее {options.Value.MaxCodeLength} символов.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            attempt.Code = code;
            attempt.Passed = request.Passed;
            attempt.ElapsedMs = request.ElapsedMs;
            await attemptStore.UpdateAsync(attempt, ct);

            var totalAttempts = await attemptStore.CountAsync(attempt.PlayerId, attempt.LevelId, ct);
            var existingProgress = await playerStore.GetProgressAsync(attempt.PlayerId, attempt.LevelId, ct);

            var progress = existingProgress ?? new LevelProgress
            {
                Id = Guid.NewGuid(),
                PlayerId = attempt.PlayerId,
                LevelId = attempt.LevelId,
                Completed = false,
                BestAttempts = 0,
                BestTimeMs = 0,
                BestScore = 0,
                TotalAttempts = 0,
                CompletedAt = null,
            };

            progress.TotalAttempts = totalAttempts;

            var score = 0;
            var personalBest = false;

            if (request.Passed)
            {
                score = ScoreCalculator.Calculate(attempt.AttemptNumber, request.ElapsedMs);

                if (!progress.Completed || score > progress.BestScore)
                {
                    personalBest = true;
                    progress.BestAttempts = attempt.AttemptNumber;
                    progress.BestTimeMs = request.ElapsedMs;
                    progress.BestScore = score;
                    progress.CompletedAt = DateTimeOffset.UtcNow;
                }

                progress.Completed = true;
            }

            await playerStore.UpsertProgressAsync(progress, ct);

            string? nextLevelId = null;
            if (request.Passed)
            {
                var level = await levelStore.GetAsync(attempt.LevelId, ct);
                if (level is not null)
                {
                    var levels = await levelStore.GetAllAsync(ct);
                    foreach (var candidate in levels)
                    {
                        if (candidate.Order == level.Order + 1)
                        {
                            nextLevelId = candidate.Id;
                            break;
                        }
                    }
                }
            }

            var response = new SubmitResultResponse(
                true,
                score,
                personalBest,
                attempt.AttemptNumber,
                nextLevelId);

            return Results.Ok(response);
        });
    }
}
