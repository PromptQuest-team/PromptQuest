using System.Text.Json;

namespace PromptQuest.Web.Models;

// Поле kind читается как строка (не enum), чтобы новые виды проверок
// не требовали правок C#. Реализация проверок находится в wwwroot/sandbox/runner.js.
public sealed class LevelCheck
{
    public string Id { get; init; } = "";
    public string Kind { get; init; } = "";
    public string? Subject { get; init; }
    public string? Target { get; init; }
    public string[]? Selectors { get; init; }
    public string? Selector { get; init; }
    public string? Property { get; init; }
    public JsonElement? Expected { get; init; }
    public string[]? ExpectedIds { get; init; }
    public string? ClassName { get; init; }
    public string? Description { get; init; }
}
