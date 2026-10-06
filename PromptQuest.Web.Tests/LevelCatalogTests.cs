using System.Text.Json;
using PromptQuest.Web.Models;
using Xunit;

namespace PromptQuest.Web.Tests;

// (а)/(б) из задания: вид сцены для ИИ не должен содержать кувшинок/данных о
// положении цели, а goal — слов и цифр, выдающих положение.
public class LevelCatalogTests
{
    private static readonly List<LevelDefinition> Levels = LoadLevels();

    private static List<LevelDefinition> LoadLevels()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "levels.json");
        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonSerializer.Deserialize<List<LevelDefinition>>(json, options) ?? new List<LevelDefinition>();
    }

    public static IEnumerable<object[]> LevelIds() => Levels.Select(l => new object[] { l.Id });

    [Fact]
    public void CatalogHasThirteenLevels()
    {
        Assert.Equal(13, Levels.Count);
    }

    [Theory]
    [MemberData(nameof(LevelIds))]
    public void AiScene_ContainsNoLilyMarkersOrPositionData(string levelId)
    {
        var level = Levels.Single(l => l.Id == levelId);

        Assert.DoesNotContain("lily", level.AiScene.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lily", level.AiScene.BaseCss, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(LevelIds))]
    public void AiScene_OnlyReferencesIdsThatExistInTheRealScene(string levelId)
    {
        var level = Levels.Single(l => l.Id == levelId);

        // Любой CSS, написанный ИИ по aiScene, должен применяться и к настоящей
        // сцене: id, упомянутые в aiScene.Html, должны существовать в scene.Html.
        Assert.NotEmpty(ExtractIds(level.AiScene.Html));
        foreach (var token in ExtractIds(level.AiScene.Html))
        {
            Assert.Contains(token, level.Scene.Html);
        }
    }

    private static IEnumerable<string> ExtractIds(string html)
    {
        var marker = "id=\"";
        var index = 0;
        while (true)
        {
            var start = html.IndexOf(marker, index, StringComparison.Ordinal);
            if (start < 0)
            {
                yield break;
            }

            start += marker.Length;
            var end = html.IndexOf('"', start);
            yield return html[start..end];
            index = end + 1;
        }
    }

    [Theory]
    [MemberData(nameof(LevelIds))]
    public void Goal_DoesNotRevealPositionWordsOrNumbers(string levelId)
    {
        var level = Levels.Single(l => l.Id == levelId);
        var goal = level.Goal.ToLowerInvariant();

        Assert.False(goal.Any(char.IsDigit), $"goal уровня {levelId} содержит цифру: \"{level.Goal}\"");

        string[] bannedStems = { "лев", "прав", "верх", "ниж", "центр", "угол", "ряд", "столб", "ячейк" };
        foreach (var stem in bannedStems)
        {
            Assert.False(
                goal.Contains(stem, StringComparison.Ordinal),
                $"goal уровня {levelId} содержит слово с корнем \"{stem}\": \"{level.Goal}\"");
        }
    }
}
