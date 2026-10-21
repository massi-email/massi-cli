namespace EmailSweepAutomation.Test.UnitTests;

using System;
using Xunit;

public class KeepRuleChoiceTests
{
    [Fact]
    public void Constructor_KeepRecentOnly_Succeeds()
    {
        var choice = new KeepRuleChoice(3, null);
        Assert.Equal(3, choice.KeepRecent);
        Assert.Null(choice.KeepRecentTime);
    }

    [Fact]
    public void Constructor_KeepRecentTimeOnly_Succeeds()
    {
        var choice = new KeepRuleChoice(null, TimeSpan.FromDays(30));
        Assert.Equal(TimeSpan.FromDays(30), choice.KeepRecentTime);
        Assert.Null(choice.KeepRecent);
    }

    [Fact]
    public void Constructor_BothSet_Throws()
    {
        Assert.Throws<ArgumentException>(() => new KeepRuleChoice(3, TimeSpan.FromDays(30)));
    }

    [Fact]
    public void Constructor_NeitherSet_Throws()
    {
        Assert.Throws<ArgumentException>(() => new KeepRuleChoice(null, null));
    }
}
