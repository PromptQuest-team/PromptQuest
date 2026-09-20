namespace PromptQuest.Web.Models;

public enum CodeSource { Manual = 0, Ai = 1 }

public sealed class Attempt
{
    public Guid Id { get; init; }
    public Guid PlayerId { get; init; }
    public string LevelId { get; init; } = "";
    public int AttemptNumber { get; init; }   // номер в текущей сессии уровня
    public string Prompt { get; init; } = ""; // в Фазе 1 может быть пустым
    public string Code { get; set; } = "";    // код, который проверялся
    public CodeSource Source { get; init; }   // Manual | Ai
    public bool? Passed { get; set; }         // null, пока результат не отправлен
    public int ElapsedMs { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}
