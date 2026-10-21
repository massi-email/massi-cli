namespace EmailSweepAutomation.Test.UnitTests;

using EmailSweepAutomation.Commands;
using Xunit;

public class RuleDescriptionsTests
{
    private static SweepBranch Flat(SweepRule rule) => rule.EffectiveBranches.Single();

    [Fact]
    public void ActionLabel_FlatDeleteWithKeepRecent()
    {
        var rule = new SweepRule { Id = 1, FromAddress = "news@shop.com", Delete = true, KeepRecent = 3 };
        Assert.Equal("Delete (keep 3 newest)", RuleDescriptions.ActionLabel(Flat(rule)));
    }

    [Fact]
    public void ActionLabel_MoveWithKeepRecentTime()
    {
        var rule = new SweepRule { Id = 1, FromAddress = "a@b.com", MoveToFolder = "Receipts", KeepRecentTime = TimeSpan.FromDays(30) };
        Assert.Equal("Move → Receipts (keep last 30d)", RuleDescriptions.ActionLabel(Flat(rule)));
    }

    [Fact]
    public void ActionLabel_Alert()
    {
        var rule = new SweepRule { Id = 1, SubjectContains = "invoice", Alert = true };
        Assert.Equal("Alert", RuleDescriptions.ActionLabel(Flat(rule)));
    }

    [Fact]
    public void ActionLabel_LeaveBranch()
    {
        Assert.Equal("Leave", RuleDescriptions.ActionLabel(new SweepBranch { Label = "Rest", Action = SweepBranchAction.Leave }));
    }

    [Fact]
    public void ActionsDescription_Flat_IsTheActionLabel()
    {
        var rule = new SweepRule { Id = 1, FromAddress = "a@b.com", Delete = true, KeepRecent = 3 };
        Assert.Equal("Delete (keep 3 newest)", RuleDescriptions.ActionsDescription(rule, Flat(rule)));
    }

    [Fact]
    public void ActionsDescription_Branched_PrefixesTheBranchLabel()
    {
        var branch = new SweepBranch { Label = "Receipts", Action = SweepBranchAction.MoveToFolder, MoveToFolder = "Shop", KeepRecentTime = TimeSpan.FromDays(30) };
        var rule = new SweepRule { Id = 1, FromDomain = "shop.com", Branches = [branch] };
        Assert.Equal("Branch Receipts: Move → Shop (keep last 30d)", RuleDescriptions.ActionsDescription(rule, branch));
    }

    [Fact]
    public void Criteria_FromDomain()
    {
        Assert.Equal("From domain shop.com", RuleDescriptions.Criteria(new SweepRule { FromDomain = "shop.com" }));
    }

    [Fact]
    public void Criteria_AllSet_JoinedWithAnd()
    {
        var rule = new SweepRule { FromAddress = "news@", SubjectContains = "sale" };
        Assert.Equal("From ~ news@ AND Subject ~ sale", RuleDescriptions.Criteria(rule));
    }

    [Fact]
    public void Criteria_None_IsNull()
    {
        Assert.Null(RuleDescriptions.Criteria(new SweepRule()));
    }

    [Fact]
    public void BranchConditions_SenderPatternsAndSubjectKeywords()
    {
        var branch = new SweepBranch { FromAny = ["promo*@shop.com", "@*.shop.com"], SubjectContainsAny = ["sale", "deal"] };
        Assert.Equal("From any of promo*@shop.com, @*.shop.com AND Subject has any of sale, deal",
            RuleDescriptions.BranchConditions(branch));
    }

    [Fact]
    public void BranchConditions_CatchAll()
    {
        Assert.Equal("(everything else)", RuleDescriptions.BranchConditions(new SweepBranch { Label = "Rest" }));
    }

    [Fact]
    public void Describe_FlatRule_HasMatchesAndAction()
    {
        var rule = new SweepRule { Id = 1, FromAddress = "news@shop.com", Delete = true, KeepRecent = 3 };

        var d = RuleDescriptions.Describe(rule);

        Assert.Equal("From ~ news@shop.com", d.Matches);
        Assert.Equal("Delete (keep 3 newest)", d.Action);
        Assert.Empty(d.Branches);
    }

    [Fact]
    public void Describe_AlertRule()
    {
        var d = RuleDescriptions.Describe(new SweepRule { Id = 2, SubjectContains = "invoice", Alert = true });

        Assert.Equal("Subject ~ invoice", d.Matches);
        Assert.Equal("Alert", d.Action);
    }

    [Fact]
    public void Describe_BranchedRule_NumbersBranchesInOrder()
    {
        var rule = new SweepRule
        {
            Id = 3,
            FromDomain = "shop.com",
            Branches =
            [
                new() { Label = "Receipts", SubjectContainsAny = ["receipt"], Action = SweepBranchAction.MoveToFolder, MoveToFolder = "Shop/Receipts", KeepRecentTime = TimeSpan.FromDays(30) },
                new() { Label = "Promos", FromAny = ["promo*@shop.com"], Action = SweepBranchAction.Delete, KeepRecent = 0 },
                new() { Label = "Rest", Action = SweepBranchAction.Leave },
            ],
        };

        var d = RuleDescriptions.Describe(rule);

        Assert.Equal("From domain shop.com", d.Matches);
        Assert.Null(d.Action);
        Assert.Equal(
            [
                "1. Receipts  Subject has any of receipt → Move → Shop/Receipts (keep last 30d)",
                "2. Promos  From any of promo*@shop.com → Delete (keep 0 newest)",
                "3. Rest  (everything else) → Leave",
            ],
            d.Branches);
    }

    [Fact]
    public void Describe_InvalidRule_StillDescribed()
    {
        var rule = new SweepRule { Id = 4, FromAddress = "a@b.com", Delete = true };

        var d = RuleDescriptions.Describe(rule);

        Assert.Equal("From ~ a@b.com", d.Matches);
        Assert.Equal("Delete", d.Action);
        Assert.NotNull(rule.Validate());
    }

    [Fact]
    public void Describe_EmptyRule_HasNothing()
    {
        var d = RuleDescriptions.Describe(new SweepRule { Id = 5 });

        Assert.Null(d.Matches);
        Assert.Null(d.Action);
        Assert.Empty(d.Branches);
    }

    [Fact]
    public void Describe_FlatDeleteFalse_ReadsAsLeave()
    {
        var rule = new SweepRule { Id = 6, FromAddress = "a@b.com", Delete = false, KeepRecent = 1 };
        Assert.Equal("Leave", RuleDescriptions.Describe(rule).Action);
    }

    [Theory]
    [InlineData(1, 0, "1 rule, 0 invalid")]
    [InlineData(5, 1, "5 rules, 1 invalid")]
    [InlineData(0, 0, "0 rules, 0 invalid")]
    public void Summary_CountsRulesAndInvalid(int rules, int invalid, string expected)
    {
        Assert.Equal(expected, RuleDescriptions.Summary(rules, invalid));
    }
}
