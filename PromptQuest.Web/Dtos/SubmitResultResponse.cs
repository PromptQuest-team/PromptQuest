namespace PromptQuest.Web.Dtos;

public sealed record SubmitResultResponse(
    bool Accepted,
    int PromptLength,
    bool PersonalBest,
    string? NextLevelId);
