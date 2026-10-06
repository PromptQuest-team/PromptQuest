using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PromptQuest.Web.Models;
using PromptQuest.Web.Services;
using PromptQuest.Web.Services.Storage;
using PromptQuest.Web.Services.Storage.Db;
using PromptQuest.Web.Services.Storage.InMemory;
using Xunit;

namespace PromptQuest.Web.Tests;

// Реальный Gemini API не вызывается ни в одном из этих тестов — вся генерация
// кода подменена FakeCodeGenerationService. PromptQuestWebFactory всегда
// подставляет in-memory хранилище (см. её ConfigureServices), так что ни один
// из HTTP-тестов ниже не подключается к реальной БД.
public class ApiIntegrationTests
{
    private static async Task<string> CreatePlayerAsync(HttpClient client, string nickname)
    {
        var response = await client.PostAsJsonAsync("/api/players", new { nickname });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("playerId").GetString()!;
    }

    // (ж) лимит запросов возвращает 429 и попытка не создаётся.
    [Fact]
    public async Task CreateAttempt_ExceedingRateLimit_Returns429WithRetryAfter()
    {
        using var factory = new PromptQuestWebFactory(new Dictionary<string, string?>
        {
            ["PromptQuest:AiRequestsPerMinutePerPlayer"] = "1",
        });
        var client = factory.CreateClient();

        var playerId = await CreatePlayerAsync(client, "RateLimited");
        var payload = new { playerId, levelId = "css-01-justify", prompt = "move the frog" };

        var first = await client.PostAsJsonAsync("/api/attempts", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/attempts", payload);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.True(second.Headers.Contains("Retry-After"), "Ожидался заголовок Retry-After на 429.");
    }

    // (г, интеграционно) сбой провайдера -> 502, попытка не создаётся (конструктор
    // эндпоинта возвращается до attemptStore.AddAsync — см. AttemptEndpoints.cs).
    [Fact]
    public async Task CreateAttempt_WhenAiFails_Returns502()
    {
        using var factory = new PromptQuestWebFactory();
        factory.CodeGen.Handler = (_, _) => new CodeGenerationResult(false, "", false, CodeSource.Ai, "simulated failure");
        var client = factory.CreateClient();

        var playerId = await CreatePlayerAsync(client, "AiFail");
        var payload = new { playerId, levelId = "css-01-justify", prompt = "move the frog" };

        var response = await client.PostAsJsonAsync("/api/attempts", payload);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task CreateAttempt_WhenAiSucceeds_Returns200WithGeneratedCode()
    {
        using var factory = new PromptQuestWebFactory();
        factory.CodeGen.Handler = (_, _) => new CodeGenerationResult(true, "#pond{justify-content:flex-end}", false, CodeSource.Ai, null);
        var client = factory.CreateClient();

        var playerId = await CreatePlayerAsync(client, "AiOk");
        var payload = new { playerId, levelId = "css-01-justify", prompt = "move the frog to the right" };

        var response = await client.PostAsJsonAsync("/api/attempts", payload);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("#pond{justify-content:flex-end}", body.GetProperty("code").GetString());
    }

    // (е) выбор реализации ILeaderboardService только по окружению/конфигурации,
    // без перекрытия одной регистрации другой (StorageRegistration.AddStorage —
    // тот же метод, который вызывает реальный Program.cs).
    [Theory]
    [InlineData(true, typeof(InMemoryLeaderboardService))]
    [InlineData(false, typeof(EfLeaderboardService))]
    public void AddStorage_SelectsLeaderboardServiceByConfiguration(bool useInMemory, Type expectedType)
    {
        var services = new ServiceCollection();
        var connectionString = useInMemory
            ? null
            : "Host=localhost;Database=promptquest_test;Username=testuser;Password=testpass";

        services.AddStorage(useInMemory, connectionString);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ILeaderboardService>();

        Assert.IsType(expectedType, service);
    }

    [Fact]
    public void AddStorage_WithoutInMemoryAndWithoutConnectionString_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() => services.AddStorage(false, null));
    }
}
