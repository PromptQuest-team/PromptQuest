namespace PromptQuest.Web.Configuration;

public sealed class AppOptions
{
    public const string SectionName = "PromptQuest";

    public bool ManualCodeEntry { get; set; } = true;
    public string LevelsFilePath { get; set; } = "Data/levels.json";
    public string GeminiModel { get; set; } = "gemini-3.1-flash-lite";
    public int MaxPromptLength { get; set; } = 2000;
    public int MaxCodeLength { get; set; } = 8000;
    public int DefaultTolerancePx { get; set; } = 8;
    public int DefaultTimeoutMs { get; set; } = 2000;
    public int LeaderboardTake { get; set; } = 20;
}
