namespace PromptQuest.Web.Dtos;

public sealed record LeaderboardEntryDto(
    int Rank,
    string Nickname,
    int PromptLength);
