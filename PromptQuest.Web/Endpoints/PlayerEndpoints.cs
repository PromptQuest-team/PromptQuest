using PromptQuest.Web.Dtos;
using PromptQuest.Web.Services.Storage;

namespace PromptQuest.Web.Endpoints;

public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this WebApplication app)
    {
        app.MapPost("/api/players", async (
            CreatePlayerRequest request,
            IPlayerStore playerStore,
            CancellationToken ct) =>
        {
            var nickname = (request.Nickname ?? "").Trim();

            if (nickname.Length < 2 || nickname.Length > 20)
            {
                return Results.Problem(
                    detail: "Никнейм должен содержать от 2 до 20 символов.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var player = await playerStore.CreateAsync(nickname, ct);

            var response = new PlayerResponse(
                player.Id,
                player.Nickname,
                new List<LevelProgressDto>());

            return Results.Ok(response);
        });

        app.MapGet("/api/players/{playerId:guid}", async (
            Guid playerId,
            IPlayerStore playerStore,
            CancellationToken ct) =>
        {
            var player = await playerStore.GetAsync(playerId, ct);

            if (player is null)
            {
                return Results.Problem(
                    detail: "Игрок не найден.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            var progressDtos = new List<LevelProgressDto>();
            foreach (var progress in player.Progress)
            {
                progressDtos.Add(new LevelProgressDto(
                    progress.LevelId,
                    progress.Completed,
                    progress.BestPromptLength,
                    progress.CompletedAt));
            }

            var response = new PlayerResponse(player.Id, player.Nickname, progressDtos);
            return Results.Ok(response);
        });
    }
}
