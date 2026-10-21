namespace EmailSweepAutomation.Test.UnitTests;

using Xunit;

public class SweepBranchTests
{
    [Fact]
    public void Matches_AnyOneSenderEntry_IsEnough()
    {
        var branch = new SweepBranch { FromAny = ["@other.com", "@shop.com"] };
        Assert.True(branch.Matches(["x@shop.com"], "hi"));
    }

    [Fact]
    public void Matches_AnyOneOfSeveralSenders_IsEnough()
    {
        var branch = new SweepBranch { FromAny = ["@shop.com"] };
        Assert.True(branch.Matches(["x@other.com", "y@shop.com"], "hi"));
    }

    [Fact]
    public void Matches_AnyOneSubjectKeyword_IsEnough()
    {
        var branch = new SweepBranch { SubjectContainsAny = ["receipt", "invoice"] };
        Assert.True(branch.Matches(["x@shop.com"], "Your INVOICE is ready"));
    }

    [Fact]
    public void Matches_WithoutASenderCondition_TheSubjectAloneDecides_EvenWithNoSender()
    {
        var branch = new SweepBranch { SubjectContainsAny = ["receipt"] };
        Assert.True(branch.Matches([], "receipt"));
        Assert.False(branch.Matches([], null));
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(new string[0], new string[0], true)] // empty lists are no condition
    [InlineData(new[] { "@shop.com" }, null, false)]
    [InlineData(null, new[] { "sale" }, false)]
    public void IsCatchAll_OnlyWithNoConditions(string[]? from, string[]? subjects, bool expected)
    {
        var branch = new SweepBranch { FromAny = from?.ToList(), SubjectContainsAny = subjects?.ToList() };
        Assert.Equal(expected, branch.IsCatchAll);
    }

    [Fact]
    public void ALabellessBranch_IsInvalid()
    {
        // The default label must be blank, so a branch read without one fails validation.
        var rule = new SweepRule
        {
            FromDomain = "shop.com",
            Branches = [new SweepBranch { FromAny = ["@mail.shop.com"], Action = SweepBranchAction.Leave }],
        };
        Assert.Equal(RuleProblemKind.UnlabelledBranch, rule.Validate()?.Kind);
    }

    [Fact]
    public void IsBranched_WithAnEmptyBranchList_IsFalse()
    {
        var rule = new SweepRule { FromAddress = "a@b.com", Delete = true, KeepRecent = 0, Branches = [] };
        Assert.False(rule.IsBranched);
        Assert.Equal(SweepBranchAction.Delete, Assert.Single(rule.EffectiveBranches).Action);
    }

    [Theory]
    [InlineData(true, null, null, SweepBranchAction.Delete)]
    [InlineData(false, null, null, SweepBranchAction.Leave)]
    [InlineData(null, "Archive", null, SweepBranchAction.MoveToFolder)]
    [InlineData(null, null, true, SweepBranchAction.Alert)]
    [InlineData(null, null, false, SweepBranchAction.Leave)]
    public void EffectiveBranches_OfAFlatRule_IsOneBranchWithItsAction(
        bool? delete, string? folder, bool? alert, SweepBranchAction expected)
    {
        var rule = new SweepRule { FromAddress = "a@b.com", Delete = delete, MoveToFolder = folder, Alert = alert };
        var branch = Assert.Single(rule.EffectiveBranches);
        Assert.Equal(expected, branch.Action);
        Assert.True(branch.IsCatchAll);
    }

    [Fact]
    public void Matches_ASenderPattern_ReachesAnAddressItWasNotWrittenFrom()
    {
        var branch = new SweepBranch { FromAny = [" *best-care-market*@deals.shop.com "] }; // entries are trimmed
        Assert.True(branch.Matches(["aeug-best-care-market16@deals.shop.com"], "hi"));
        Assert.False(branch.Matches(["transaction@deals.shop.com"], "hi"));
    }
}
