namespace EmailSweepAutomation.Test.UnitTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MailKit;
using Xunit;

public class InteractiveRuleBuilderTests : IDisposable
{
    private readonly string _file;

    public InteractiveRuleBuilderTests()
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

    private static SenderSummary Sender(string address, int count = 5)
        => new(address, count, DateTimeOffset.Now, DateTimeOffset.Now.AddDays(-30), null, null);

    [Fact]
    public async Task RunAsync_BuildsDeleteKeepRecentRule_AndSaves()
    {
        var repo = new SweepRuleRepository(_file);
        var prompter = new FakeRulePrompter(
            new SenderSummary?[] { Sender("sender6@example.com") },
            new[] { false })
        {
            Subject = null,
            Keep = new KeepRuleChoice(3, null),
            Action = new RuleActionChoice(true, null),
        };

        await new InteractiveRuleBuilder(prompter)
            .RunAsync(new[] { Sender("sender6@example.com") }, repo);

        var rule = Assert.Single(await repo.GetRulesAsync());
        Assert.Equal("sender6@example.com", rule.FromAddress);
        Assert.Equal(3, rule.KeepRecent);
        Assert.True(rule.Delete);
        Assert.Null(rule.SubjectContains);
        Assert.Null(rule.KeepRecentTime);
        Assert.Null(rule.MoveToFolder);
    }

    [Fact]
    public async Task RunAsync_WithSubject_SetsSubjectContains()
    {
        var repo = new SweepRuleRepository(_file);
        var prompter = new FakeRulePrompter(
            new SenderSummary?[] { Sender("a@example.com") },
            new[] { false }) { Subject = "Receipt" };

        await new InteractiveRuleBuilder(prompter)
            .RunAsync(new[] { Sender("a@example.com") }, repo);

        var rule = Assert.Single(await repo.GetRulesAsync());
        Assert.Equal("Receipt", rule.SubjectContains);
    }

    [Fact]
    public async Task RunAsync_BlankSubject_LeavesSubjectNull()
    {
        var repo = new SweepRuleRepository(_file);
        var prompter = new FakeRulePrompter(
            new SenderSummary?[] { Sender("a@example.com") },
            new[] { false }) { Subject = "   " };

        await new InteractiveRuleBuilder(prompter)
            .RunAsync(new[] { Sender("a@example.com") }, repo);

        var rule = Assert.Single(await repo.GetRulesAsync());
        Assert.Null(rule.SubjectContains);
    }

    [Fact]
    public async Task RunAsync_DurationKeepRule_SetsKeepRecentTime()
    {
        var repo = new SweepRuleRepository(_file);
        var prompter = new FakeRulePrompter(
            new SenderSummary?[] { Sender("a@example.com") },
            new[] { false })
        {
            Keep = new KeepRuleChoice(null, TimeSpan.FromDays(30)),
        };

        await new InteractiveRuleBuilder(prompter)
            .RunAsync(new[] { Sender("a@example.com") }, repo);

        var rule = Assert.Single(await repo.GetRulesAsync());
        Assert.Equal(TimeSpan.FromDays(30), rule.KeepRecentTime);
        Assert.Null(rule.KeepRecent);
    }

    [Fact]
    public async Task RunAsync_MoveAction_SetsMoveToFolder()
    {
        var repo = new SweepRuleRepository(_file);
        var prompter = new FakeRulePrompter(
            new SenderSummary?[] { Sender("a@example.com") },
            new[] { false })
        {
            Action = new RuleActionChoice(false, "Archive"),
        };

        await new InteractiveRuleBuilder(prompter)
            .RunAsync(new[] { Sender("a@example.com") }, repo);

        var rule = Assert.Single(await repo.GetRulesAsync());
        Assert.Equal("Archive", rule.MoveToFolder);
        Assert.Null(rule.Delete);
    }

    [Fact]
    public async Task RunAsync_DeclineConfirm_DoesNotSave()
    {
        var repo = new SweepRuleRepository(_file);
        var prompter = new FakeRulePrompter(
            new SenderSummary?[] { Sender("a@example.com") },
            new[] { false }) { ConfirmFunc = _ => false };

        await new InteractiveRuleBuilder(prompter)
            .RunAsync(new[] { Sender("a@example.com") }, repo);

        Assert.Empty(await repo.GetRulesAsync());
        Assert.False(File.Exists(_file));
    }

    [Fact]
    public async Task RunAsync_TwoSenders_SavesTwoRulesWithDistinctIds()
    {
        var repo = new SweepRuleRepository(_file);
        var prompter = new FakeRulePrompter(
            new SenderSummary?[] { Sender("a@example.com"), Sender("b@example.com") },
            new[] { true, false });

        await new InteractiveRuleBuilder(prompter)
            .RunAsync(new[] { Sender("a@example.com"), Sender("b@example.com") }, repo);

        var rules = (await repo.GetRulesAsync()).ToList();
        Assert.Equal(2, rules.Count);
        Assert.Equal(new[] { 1, 2 }, rules.Select(r => r.Id).ToArray());
        Assert.Equal(new[] { "a@example.com", "b@example.com" },
            rules.Select(r => r.FromAddress).ToArray());
    }

    [Fact]
    public async Task RunAsync_DoneImmediately_SavesNothing()
    {
        var repo = new SweepRuleRepository(_file);
        var prompter = new FakeRulePrompter(
            new SenderSummary?[] { null },
            Array.Empty<bool>());

        await new InteractiveRuleBuilder(prompter)
            .RunAsync(new[] { Sender("a@example.com") }, repo);

        Assert.Empty(await repo.GetRulesAsync());
    }

    [Fact]
    public async Task RunAsync_InvalidRule_NotSavedAndReported()
    {
        var repo = new SweepRuleRepository(_file);
        var prompter = new FakeRulePrompter(
            new SenderSummary?[] { Sender("a@example.com") },
            new[] { false })
        {
            Action = new RuleActionChoice(false, null), // no action -> invalid
        };

        await new InteractiveRuleBuilder(prompter)
            .RunAsync(new[] { Sender("a@example.com") }, repo);

        Assert.Empty(await repo.GetRulesAsync());
        Assert.Single(prompter.InvalidMessages);
    }

    [Fact]
    public async Task RunAsync_AfterInvalidRule_ContinuesAndSavesNextValidRule()
    {
        var repo = new SweepRuleRepository(_file);
        var prompter = new FakeRulePrompter(
            new SenderSummary?[] { Sender("bad@example.com"), Sender("good@example.com") },
            new[] { true, false })
        {
            ActionQueue = new Queue<RuleActionChoice>(new[]
            {
                new RuleActionChoice(false, null),   // first rule: no action -> invalid
                new RuleActionChoice(true, null),    // second rule: delete -> valid
            }),
        };

        await new InteractiveRuleBuilder(prompter)
            .RunAsync(
                new[] { Sender("bad@example.com"), Sender("good@example.com") }, repo);

        var rule = Assert.Single(await repo.GetRulesAsync());
        Assert.Equal("good@example.com", rule.FromAddress);
        Assert.True(rule.Delete);
        Assert.Single(prompter.InvalidMessages);
    }

    [Fact]
    public async Task RunAsync_PreservesExistingRules()
    {
        await File.WriteAllTextAsync(_file,
            "[{\"Id\":1,\"FromAddress\":\"old@example.com\",\"KeepRecent\":1,\"Delete\":true}]");
        var repo = new SweepRuleRepository(_file);
        var prompter = new FakeRulePrompter(
            new SenderSummary?[] { Sender("new@example.com") },
            new[] { false });

        await new InteractiveRuleBuilder(prompter)
            .RunAsync(new[] { Sender("new@example.com") }, repo);

        var rules = (await new SweepRuleRepository(_file).GetRulesAsync()).ToList();
        Assert.Equal(2, rules.Count);
        Assert.Contains(rules, r => r.FromAddress == "old@example.com");
        Assert.Contains(rules, r => r.FromAddress == "new@example.com" && r.Id == 2);
    }

    [Fact]
    public async Task RunAsync_WhitespaceMoveFolder_NotSavedAndReported()
    {
        var repo = new SweepRuleRepository(_file);
        var prompter = new FakeRulePrompter(
            new SenderSummary?[] { Sender("a@example.com") },
            new[] { false })
        {
            Action = new RuleActionChoice(false, "   "),
        };

        await new InteractiveRuleBuilder(prompter)
            .RunAsync(new[] { Sender("a@example.com") }, repo);

        Assert.Empty(await repo.GetRulesAsync());
        Assert.Single(prompter.InvalidMessages);
    }

    [Fact]
    public async Task RunAsync_AlertAction_SavesAlertRuleAndSkipsKeepPrompt()
    {
        var repo = new SweepRuleRepository(_file);
        var prompter = new FakeRulePrompter(
            new SenderSummary?[] { Sender("boss@example.com") },
            new[] { false })
        {
            Action = new RuleActionChoice(false, null, Alert: true),
        };

        await new InteractiveRuleBuilder(prompter)
            .RunAsync(new[] { Sender("boss@example.com") }, repo);

        var rule = Assert.Single(await repo.GetRulesAsync());
        Assert.True(rule.Alert);
        Assert.Null(rule.Delete);
        Assert.Null(rule.MoveToFolder);
        Assert.Null(rule.KeepRecent);
        Assert.Null(rule.KeepRecentTime);
        Assert.Equal(0, prompter.KeepPromptCalls);
    }

    private sealed class FakeRulePrompter : IRulePrompter
    {
        private readonly Queue<SenderSummary?> sendersToSelect;
        private readonly Queue<bool> addAnotherAnswers;

        public string? Subject { get; set; }
        public KeepRuleChoice Keep { get; set; } = new(3, null);
        public RuleActionChoice Action { get; set; } = new(true, null);
        public Queue<RuleActionChoice>? ActionQueue { get; set; }
        public Func<SweepRule, bool> ConfirmFunc { get; set; } = _ => true;
        public List<string> InvalidMessages { get; } = new();

        public FakeRulePrompter(
            IEnumerable<SenderSummary?> sendersToSelect,
            IEnumerable<bool> addAnotherAnswers)
        {
            this.sendersToSelect = new Queue<SenderSummary?>(sendersToSelect);
            this.addAnotherAnswers = new Queue<bool>(addAnotherAnswers);
        }

        public SenderSummary? SelectSender(IReadOnlyList<SenderSummary> senders)
            => sendersToSelect.Count > 0 ? sendersToSelect.Dequeue() : null;

        public int KeepPromptCalls { get; private set; }

        public string? PromptSubjectContains() => Subject;
        public KeepRuleChoice PromptKeepRule()
        {
            KeepPromptCalls++;
            return Keep;
        }
        public RuleActionChoice PromptAction()
            => ActionQueue is { Count: > 0 } ? ActionQueue.Dequeue() : Action;
        public bool ConfirmRule(SweepRule rule) => ConfirmFunc(rule);
        public void ReportInvalid(string message) => InvalidMessages.Add(message);
        public bool AskAddAnother()
            => addAnotherAnswers.Count > 0 && addAnotherAnswers.Dequeue();
    }
}
