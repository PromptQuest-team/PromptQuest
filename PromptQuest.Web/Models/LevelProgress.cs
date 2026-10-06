namespace PromptQuest.Web.Models;

public sealed class LevelProgress
{
    public Guid Id { get; init; }          // собственный ключ (нужен БД)
    public Guid PlayerId { get; init; }    // внешний ключ
    public string LevelId { get; init; } = "";
    public bool Completed { get; set; }

    // Единственная метрика рекорда: длина промта (в Unicode-символах, после Trim)
    // успешной попытки. Время и число попыток больше не участвуют в рекордах —
    // поля ниже оставлены в схеме (уже сохранённые данные не удаляются), но
    // новой логикой не вычисляются.
    public int BestAttempts { get; set; }
    public int BestTimeMs { get; set; }
    public int BestScore { get; set; }
    public int TotalAttempts { get; set; }
    public int BestPromptLength { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
