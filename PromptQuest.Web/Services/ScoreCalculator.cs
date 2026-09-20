namespace PromptQuest.Web.Services;

public static class ScoreCalculator
{
    // score = Max(0, 1000 - (attempts - 1) * 75 - (elapsedMs / 1000) * 2)
    // результат округляется до целого вниз
    public static int Calculate(int attempts, int elapsedMs)
    {
        var raw = 1000.0 - (attempts - 1) * 75.0 - (elapsedMs / 1000.0) * 2.0;
        var floored = Math.Floor(raw);
        return (int)Math.Max(0.0, floored);
    }
}
