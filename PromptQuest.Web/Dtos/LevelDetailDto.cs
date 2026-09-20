using PromptQuest.Web.Models;

namespace PromptQuest.Web.Dtos;

public sealed record LevelDetailDto(
    string Id,
    int Order,
    string Category,
    string Title,
    string Goal,
    string Hint,
    int Difficulty,
    string InjectionMode,
    LevelScene Scene,
    string CodeTemplate,
    LevelValidation Validation,
    List<string> ForbiddenPatterns,
    bool ManualEntry);
