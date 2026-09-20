using PromptQuest.Web.Models;

namespace PromptQuest.Web.Services;

// SPEC-ADDENDUM-01, раздел A (седьмой пункт контракта совместимости из раздела 4.4):
// реализация GenerateAsync в Фазе 2 обязана отправлять AI API только level.SystemPrompt,
// prompt (текст игрока) и level.Scene.Html. Поля level.Goal и level.Hint передавать
// в запрос НЕЛЬЗЯ ни в каком виде (ни в системный промт, ни в пользовательское
// сообщение) — это подсказки для игрока, а не для модели. Если модель получит Goal/Hint,
// она решит уровень по описанию цели в обход промта игрока, и тренируемый навык
// (точная постановка задачи) перестанет работать. В Фазе 1 неприменимо технически,
// т.к. AI ещё не подключён (см. ManualCodeGenerationService).
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
