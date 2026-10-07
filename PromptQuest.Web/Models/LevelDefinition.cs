namespace PromptQuest.Web.Models;

public sealed class LevelDefinition
{
    public string Id { get; init; } = "";
    public int Order { get; init; }
    public string Category { get; init; } = "";
    public string Title { get; init; } = "";
    public string Goal { get; init; } = "";
    public string Hint { get; init; } = "";
    public int Difficulty { get; init; }
    public string InjectionMode { get; init; } = "";
    public LevelScene Scene { get; init; } = new();

    // «Вид сцены для ИИ» — ограниченная разметка/CSS без кувшинок и без данных,
    // по которым можно вывести положение цели. Это единственное описание сцены,
    // уходящее в запрос к ИИ (см. AI/AiAgent.cs). Полная Scene остаётся только
    // для рендера игроку и для валидатора.
    public LevelScene AiScene { get; init; } = new();

    public string CodeTemplate { get; init; } = "";
    public string SystemPrompt { get; init; } = "";
    public LevelValidation Validation { get; init; } = new();
    public List<string> ForbiddenPatterns { get; init; } = new();
}
