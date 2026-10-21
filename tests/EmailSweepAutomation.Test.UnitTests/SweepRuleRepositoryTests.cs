namespace EmailSweepAutomation.Test.UnitTests;

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

public class SweepRuleRepositoryTests : IDisposable
{
    private readonly string _file;

    public SweepRuleRepositoryTests()
    {
        _file = Path.Combine(Path.GetTempPath(), $"sweep-rules-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_file))
        {
            File.Delete(_file);
        }
    }

    #region Read

    [Fact]
    public void Constructor_NullOrEmptyFile_Throws()
    {
        Assert.Throws<ArgumentException>(() => new SweepRuleRepository(null!));
        Assert.Throws<ArgumentException>(() => new SweepRuleRepository(string.Empty));
    }

    [Fact]
    public async Task GetRulesAsync_FileDoesNotExist_ReturnsEmpty()
    {
        var repo = new SweepRuleRepository(_file);

        var rules = await repo.GetRulesAsync();

        Assert.Empty(rules);
    }

    [Fact]
    public async Task GetRulesAsync_ReadsRulesFromFile()
    {
        await File.WriteAllTextAsync(_file,
            "[{\"Id\":1,\"FromAddress\":\"a@example.com\",\"KeepRecent\":3,\"Delete\":true}]");
        var repo = new SweepRuleRepository(_file);

        var rules = (await repo.GetRulesAsync()).ToList();

        var rule = Assert.Single(rules);
        Assert.Equal(1, rule.Id);
        Assert.Equal("a@example.com", rule.FromAddress);
        Assert.Equal(3, rule.KeepRecent);
        Assert.True(rule.Delete);
    }

    [Fact]
    public void GetRulesFromString_ParsesKeepRecentTimeSpan()
    {
        var repo = new SweepRuleRepository(_file);

        var rules = repo.GetRulesFromString(
            "[{\"Id\":1,\"FromAddress\":\"a@example.com\",\"KeepRecentTime\":\"7:00:00:00.0000000\",\"Delete\":true}]")
            .ToList();

        var rule = Assert.Single(rules);
        Assert.Equal(TimeSpan.FromDays(7), rule.KeepRecentTime);
    }

    #endregion

    #region Write

    [Fact]
    public async Task AddRuleAsync_AssignsSequentialIds()
    {
        var repo = new SweepRuleRepository(_file);

        await repo.AddRuleAsync(new SweepRule { FromAddress = "a@example.com", KeepRecent = 1, Delete = true });
        await repo.AddRuleAsync(new SweepRule { FromAddress = "b@example.com", KeepRecent = 1, Delete = true });

        var ids = (await repo.GetRulesAsync()).Select(r => r.Id).ToArray();
        Assert.Equal(new[] { 1, 2 }, ids);
    }

    [Fact]
    public async Task AddRuleAsync_IdFollowsTheHighestExistingId()
    {
        // With ids 2 and 3 left after a delete, the next id must be 4. Anything derived from the
        // lowest id or the count would reuse 3, and two rules sharing an id makes update and
        // delete act on the wrong one.
        var repo = new SweepRuleRepository(_file);
        await repo.AddRuleAsync(new SweepRule { FromAddress = "a@example.com", KeepRecent = 1, Delete = true });
        await repo.AddRuleAsync(new SweepRule { FromAddress = "b@example.com", KeepRecent = 1, Delete = true });
        await repo.AddRuleAsync(new SweepRule { FromAddress = "c@example.com", KeepRecent = 1, Delete = true });
        await repo.DeleteRuleAsync(1);

        await repo.AddRuleAsync(new SweepRule { FromAddress = "d@example.com", KeepRecent = 1, Delete = true });

        // Sorted: this is about which id is allocated, not the order the repository returns rules in.
        var ids = (await repo.GetRulesAsync()).Select(r => r.Id).Order().ToArray();
        Assert.Equal(new[] { 2, 3, 4 }, ids);
    }

    [Fact]
    public async Task AddRulesAsync_AddsAll()
    {
        var repo = new SweepRuleRepository(_file);

        await repo.AddRulesAsync(new[]
        {
            new SweepRule { FromAddress = "a@example.com", KeepRecent = 1, Delete = true },
            new SweepRule { FromAddress = "b@example.com", KeepRecent = 1, Delete = true }
        });

        Assert.Equal(2, (await repo.GetRulesAsync()).Count());
    }

    [Fact]
    public async Task SaveAsync_PersistsRulesAcrossInstances()
    {
        var repo = new SweepRuleRepository(_file);
        await repo.AddRuleAsync(new SweepRule { FromAddress = "a@example.com", KeepRecentTime = TimeSpan.FromDays(5), Delete = true });
        await repo.SaveAsync();

        var reloaded = (await new SweepRuleRepository(_file).GetRulesAsync()).ToList();

        var rule = Assert.Single(reloaded);
        Assert.Equal("a@example.com", rule.FromAddress);
        Assert.Equal(TimeSpan.FromDays(5), rule.KeepRecentTime);
    }

    [Fact]
    public async Task UpdateRuleAsync_ReplacesExistingRule()
    {
        var repo = new SweepRuleRepository(_file);
        await repo.AddRuleAsync(new SweepRule { FromAddress = "a@example.com", KeepRecent = 1, Delete = true });

        await repo.UpdateRuleAsync(new SweepRule { Id = 1, FromAddress = "a@example.com", KeepRecent = 9, Delete = true });

        var rule = Assert.Single(await repo.GetRulesAsync());
        Assert.Equal(9, rule.KeepRecent);
    }

    [Fact]
    public async Task UpdateRuleAsync_UnknownId_Throws()
    {
        var repo = new SweepRuleRepository(_file);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.UpdateRuleAsync(new SweepRule { Id = 42, FromAddress = "a@example.com", KeepRecent = 1, Delete = true }));
    }

    [Fact]
    public async Task DeleteRuleAsync_RemovesRule()
    {
        var repo = new SweepRuleRepository(_file);
        await repo.AddRuleAsync(new SweepRule { FromAddress = "a@example.com", KeepRecent = 1, Delete = true });
        await repo.AddRuleAsync(new SweepRule { FromAddress = "b@example.com", KeepRecent = 1, Delete = true });

        await repo.DeleteRuleAsync(1);

        var rule = Assert.Single(await repo.GetRulesAsync());
        Assert.Equal(2, rule.Id);
    }

    [Fact]
    public async Task DeleteRuleAsync_UnknownId_Throws()
    {
        var repo = new SweepRuleRepository(_file);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.DeleteRuleAsync(42));
    }

    #endregion

    #region Branched Rules

    [Fact]
    public void ABranchedRule_RoundTripsThroughTheRulesFileFormat_WithTheActionAsAString()
    {
        const string json = """
            [{ "Id": 1, "FromDomain": "shop.com", "Branches": [
                { "Label": "Orders", "FromAny": ["transaction@notice.shop.com"], "Action": "MoveToFolder",
                  "MoveToFolder": "Massi/Orders", "KeepRecentTime": "30.00:00:00" },
                { "Label": "Promotions", "FromAny": ["@mail.shop.com"], "Action": "Delete", "KeepRecent": 3 } ] }]
            """;
        var repo = new SweepRuleRepository(_file);
        var rule = Assert.Single(repo.GetRulesFromString(json));
        Assert.Equal("shop.com", rule.FromDomain);
        Assert.Equal(SweepBranchAction.MoveToFolder, rule.Branches![0].Action);
        Assert.Equal(TimeSpan.FromDays(30), rule.Branches[0].KeepRecentTime);
        Assert.Equal(3, rule.Branches[1].KeepRecent);
    }

    [Fact]
    public void ABranchMissingItsAction_IsLeave_NeverDelete()
    {
        var repo = new SweepRuleRepository(_file);
        var rule = Assert.Single(repo.GetRulesFromString(
            """[{ "Id": 1, "FromDomain": "shop.com", "Branches": [{ "Label": "X", "FromAny": ["@a.shop.com"] }] }]"""));
        Assert.Equal(SweepBranchAction.Leave, rule.Branches![0].Action);
    }

    [Fact]
    public void AFlatRule_SerialisesWithoutTheNewKeys()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new SweepRule { FromAddress = "a@b.com", Delete = true, KeepRecent = 1 });
        Assert.DoesNotContain("FromDomain", json);
        Assert.DoesNotContain("Branches", json);
    }

    #endregion
}
