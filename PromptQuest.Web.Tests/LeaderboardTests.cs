using PromptQuest.Web.Models;
using PromptQuest.Web.Services.Storage.InMemory;
using Xunit;

namespace PromptQuest.Web.Tests;

// (д) подсчёт длины и сортировка лидерборда: по возрастанию длины промта,
// при равенстве — раньше достигший выше.
public class LeaderboardTests
{
    [Fact]
    public async Task GetForLevelAsync_SortsByPromptLengthThenByEarlierCompletion()
    {
        var playerStore = new InMemoryPlayerStore();
        var leaderboard = new InMemoryLeaderboardService(playerStore);

        var alice = await playerStore.CreateAsync("Alice");
        var bob = await playerStore.CreateAsync("Bob");
        var carol = await playerStore.CreateAsync("Carol");

        var now = DateTimeOffset.UtcNow;

        await playerStore.UpsertProgressAsync(new LevelProgress
        {
            Id = Guid.NewGuid(),
            PlayerId = alice.Id,
            LevelId = "css-01-justify",
            Completed = true,
            BestPromptLength = 30,
            CompletedAt = now,
        });
        await playerStore.UpsertProgressAsync(new LevelProgress
        {
            Id = Guid.NewGuid(),
            PlayerId = bob.Id,
            LevelId = "css-01-justify",
            Completed = true,
            BestPromptLength = 10,
            CompletedAt = now.AddMinutes(1),
        });
        await playerStore.UpsertProgressAsync(new LevelProgress
        {
            Id = Guid.NewGuid(),
            PlayerId = carol.Id,
            LevelId = "css-01-justify",
            Completed = true,
            BestPromptLength = 10, // ту же длину, что у Bob, но достигла раньше
            CompletedAt = now,
        });

        var entries = await leaderboard.GetForLevelAsync("css-01-justify", 10);

        Assert.Equal(3, entries.Count);

        Assert.Equal("Carol", entries[0].Nickname);
        Assert.Equal(10, entries[0].PromptLength);
        Assert.Equal(1, entries[0].Rank);

        Assert.Equal("Bob", entries[1].Nickname);
        Assert.Equal(10, entries[1].PromptLength);
        Assert.Equal(2, entries[1].Rank);

        Assert.Equal("Alice", entries[2].Nickname);
        Assert.Equal(30, entries[2].PromptLength);
        Assert.Equal(3, entries[2].Rank);
    }

    [Fact]
    public async Task GetForLevelAsync_ExcludesIncompleteProgressAndOtherLevels()
    {
        var playerStore = new InMemoryPlayerStore();
        var leaderboard = new InMemoryLeaderboardService(playerStore);

        var dave = await playerStore.CreateAsync("Dave");

        await playerStore.UpsertProgressAsync(new LevelProgress
        {
            Id = Guid.NewGuid(),
            PlayerId = dave.Id,
            LevelId = "css-01-justify",
            Completed = false,
            BestPromptLength = 0,
        });
        await playerStore.UpsertProgressAsync(new LevelProgress
        {
            Id = Guid.NewGuid(),
            PlayerId = dave.Id,
            LevelId = "css-02-align",
            Completed = true,
            BestPromptLength = 5,
            CompletedAt = DateTimeOffset.UtcNow,
        });

        var entries = await leaderboard.GetForLevelAsync("css-01-justify", 10);

        Assert.Empty(entries);
    }
}
