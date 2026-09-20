namespace PromptQuest.Web.Models;

public sealed class Player
{
    public Guid Id { get; init; }
    public string Nickname { get; set; } = "";
    public DateTimeOffset CreatedAt { get; init; }

    // Коллекция, а НЕ Dictionary: словарь не отображается в таблицу БД
    public ICollection<LevelProgress> Progress { get; } = new List<LevelProgress>();
}
