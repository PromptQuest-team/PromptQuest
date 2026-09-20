namespace PromptQuest.Web.Dtos;

public sealed record LeaderboardEntryDto(
    int Rank,
    string Nickname,
    int Attempts,
    int TimeMs,
    int Score);
