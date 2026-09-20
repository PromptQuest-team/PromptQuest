namespace PromptQuest.Web.Dtos;

public sealed record CreateAttemptResponse(
    Guid AttemptId,
    int AttemptNumber,
    string Code,
    string Source,
    bool ManualEntry);
