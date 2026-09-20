namespace PromptQuest.Web.Dtos;

public sealed record CheckResultDto(string Id, bool Passed);

public sealed record SubmitResultRequest(
    bool Passed,
    int ElapsedMs,
    string Code,
    List<CheckResultDto>? Checks);
