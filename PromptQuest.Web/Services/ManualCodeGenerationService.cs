using PromptQuest.Web.Models;

namespace PromptQuest.Web.Services;

// Заглушка Фазы 1. При замене на реальный AI-вызов в Фазе 2 см. правило в
// ICodeGenerationService.cs (SPEC-ADDENDUM-01, раздел A) — level.Goal/level.Hint
// не должны попадать в запрос к модели, только level.SystemPrompt, prompt и level.Scene.Html.
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
