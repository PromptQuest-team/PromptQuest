namespace PromptQuest.Web.Models;

public sealed class LevelValidation
{
    public int TolerancePx { get; init; } = 8;
    public int TimeoutMs { get; init; } = 2000;
    public List<LevelCheck> Checks { get; init; } = new();
}
