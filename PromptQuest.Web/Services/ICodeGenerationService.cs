using PromptQuest.Web.Models;

namespace PromptQuest.Web.Services;

// Реализация GenerateAsync обязана отправлять AI API только level.SystemPrompt,
// prompt (текст игрока) и level.AiScene.Html/BaseCss. Поля level.Goal и level.Hint
// передавать в запрос НЕЛЬЗЯ ни в каком виде (ни в системный промт, ни в
// пользовательское сообщение) — это подсказки для игрока, а не для модели. Если
// модель получит Goal/Hint, она решит уровень по описанию цели в обход промта
// игрока, и тренируемый навык (точная постановка задачи) перестанет работать.
public interface ICodeGenerationService
{
    Task<CodeGenerationResult> GenerateAsync(
        LevelDefinition level, string prompt, CancellationToken ct = default);
}

// Успех/отказ выражен в типе результата, а не исключением
public sealed record CodeGenerationResult(
    bool       Success,
    string     Code,        // пусто в Фазе 1 и при отказе
    bool       ManualEntry, // true в Фазе 1
    CodeSource Source,      // Manual | Ai
    string?    Error);      // причина отказа, иначе null
