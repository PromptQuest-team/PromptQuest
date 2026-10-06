using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PromptQuest.Web.AI;
using PromptQuest.Web.Configuration;
using PromptQuest.Web.Models;
using PromptQuest.Web.Services;
using Xunit;

namespace PromptQuest.Web.Tests;

// (в) подстановка плейсхолдера {CURRENT_CSS}; (г) сбой провайдера (исключение,
// таймаут, пустой ответ) даёт Success=false, пустой Code, причину в Error —
// никогда текст ошибки в Code.
public class AiAgentTests
{
    private static LevelDefinition SampleLevel(string systemPrompt = "sys {CURRENT_CSS}") => new()
    {
        Id = "test-level",
        SystemPrompt = systemPrompt,
        Goal = "SECRET_GOAL_TOKEN",
        Hint = "SECRET_HINT_TOKEN",
        AiScene = new LevelScene { Html = "<div id=\"pond\"></div>", BaseCss = "#pond{color:red}" },
    };

    [Fact]
    public void BuildSystemInstruction_ResolvesCurrentCssPlaceholder()
    {
        var instruction = AiAgent.BuildSystemInstruction(SampleLevel());

        Assert.Contains("#pond{color:red}", instruction);
        Assert.DoesNotContain("{CURRENT_CSS}", instruction);
        Assert.Contains("<div id=\"pond\"></div>", instruction);
    }

    [Fact]
    public void BuildSystemInstruction_NeverIncludesGoalOrHint()
    {
        var instruction = AiAgent.BuildSystemInstruction(SampleLevel());

        Assert.DoesNotContain("SECRET_GOAL_TOKEN", instruction);
        Assert.DoesNotContain("SECRET_HINT_TOKEN", instruction);
    }

    private sealed class ThrowingAiAgent : AiAgent
    {
        public ThrowingAiAgent(IOptions<AppOptions> options)
            : base(null!, new ConfigurationBuilder().Build(), options, NullLogger<AiAgent>.Instance)
        {
        }

        protected internal override Task<string> AskAsync(LevelDefinition level, string prompt, CancellationToken ct)
            => throw new InvalidOperationException("Simulated provider failure.");
    }

    private sealed class HangingAiAgent : AiAgent
    {
        public HangingAiAgent(IOptions<AppOptions> options)
            : base(null!, new ConfigurationBuilder().Build(), options, NullLogger<AiAgent>.Instance)
        {
        }

        protected internal override async Task<string> AskAsync(LevelDefinition level, string prompt, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return "unreachable";
        }
    }

    private sealed class EmptyResponseAiAgent : AiAgent
    {
        public EmptyResponseAiAgent(IOptions<AppOptions> options)
            : base(null!, new ConfigurationBuilder().Build(), options, NullLogger<AiAgent>.Instance)
        {
        }

        protected internal override Task<string> AskAsync(LevelDefinition level, string prompt, CancellationToken ct)
            => Task.FromResult("   ");
    }

    [Fact]
    public async Task GenerateAsync_ProviderException_ReturnsFailureWithoutErrorTextInCode()
    {
        var agent = new ThrowingAiAgent(Options.Create(new AppOptions()));

        var result = await agent.GenerateAsync(SampleLevel(), "move the frog");

        Assert.False(result.Success);
        Assert.Equal("", result.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task GenerateAsync_Timeout_ReturnsFailureWithoutErrorTextInCode()
    {
        var agent = new HangingAiAgent(Options.Create(new AppOptions { AiRequestTimeoutMs = 50 }));

        var result = await agent.GenerateAsync(SampleLevel(), "move the frog");

        Assert.False(result.Success);
        Assert.Equal("", result.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task GenerateAsync_EmptyResponse_ReturnsFailureWithoutErrorTextInCode()
    {
        var agent = new EmptyResponseAiAgent(Options.Create(new AppOptions()));

        var result = await agent.GenerateAsync(SampleLevel(), "move the frog");

        Assert.False(result.Success);
        Assert.Equal("", result.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task GenerateAsync_EmptyPrompt_ReturnsFailureWithoutCallingProvider()
    {
        var agent = new ThrowingAiAgent(Options.Create(new AppOptions()));

        var result = await agent.GenerateAsync(SampleLevel(), "   ");

        Assert.False(result.Success);
        Assert.Equal("", result.Code);
    }
}
