namespace PromptQuest.Web.Dtos;

public sealed record LevelProgressDto(
    string LevelId,
    bool Completed,
    int BestPromptLength,
    DateTimeOffset? CompletedAt);

public sealed record PlayerResponse(
    Guid PlayerId,
    string Nickname,
    IReadOnlyList<LevelProgressDto> Progress);
