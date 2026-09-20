namespace PromptQuest.Web.Dtos;

public sealed record CreateAttemptRequest(Guid PlayerId, string LevelId, string? Prompt);
