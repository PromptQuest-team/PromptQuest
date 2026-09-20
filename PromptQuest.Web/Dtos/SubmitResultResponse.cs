namespace PromptQuest.Web.Dtos;

public sealed record SubmitResultResponse(
    bool Accepted,
    int Score,
    bool PersonalBest,
    int Attempts,
    string? NextLevelId);
