namespace PromptQuest.Web.Dtos;

public sealed record LevelProgressDto(
    string LevelId,
    bool Completed,
    int BestAttempts,
    int BestTimeMs,
    int BestScore,
    int TotalAttempts,
    DateTimeOffset? CompletedAt);

public sealed record PlayerResponse(
    Guid PlayerId,
    string Nickname,
    IReadOnlyList<LevelProgressDto> Progress);
