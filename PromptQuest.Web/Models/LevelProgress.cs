namespace PromptQuest.Web.Models;

public sealed class LevelProgress
{
    public Guid Id { get; init; }          // собственный ключ (нужен БД)
    public Guid PlayerId { get; init; }    // внешний ключ
    public string LevelId { get; init; } = "";
    public bool Completed { get; set; }
    public int BestAttempts { get; set; }  // попыток в лучшем прохождении
    public int BestTimeMs { get; set; }    // время лучшего прохождения
    public int BestScore { get; set; }     // очки лучшего прохождения
    public int TotalAttempts { get; set; } // всего попыток по уровню
    public DateTimeOffset? CompletedAt { get; set; }
}
