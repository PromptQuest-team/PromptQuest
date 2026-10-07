using PromptQuest.Web.Models;

namespace PromptQuest.Web.Services;

// Заглушка ручного ввода кода — не вызывает ИИ. Не зарегистрирована в DI (см.
// AiAgent.cs); правило об изоляции level.Goal/level.Hint от запроса к модели
// задокументировано в ICodeGenerationService.cs.
public sealed class ManualCodeGenerationService : ICodeGenerationService
{
    public Task<CodeGenerationResult> GenerateAsync(
        LevelDefinition level, string prompt, CancellationToken ct = default)
    {
        var result = new CodeGenerationResult(
            Success: true,
            Code: "",
            ManualEntry: true,
            Source: CodeSource.Manual,
            Error: null);

        return Task.FromResult(result);
    }
}
