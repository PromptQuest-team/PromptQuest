namespace PromptQuest.Web.Dtos;

public sealed record LevelSummaryDto(
    string Id,
    int Order,
    string Category,
    string Title,
    int Difficulty,
    bool Completed,
    int BestPromptLength,
    bool ManualEntry);
