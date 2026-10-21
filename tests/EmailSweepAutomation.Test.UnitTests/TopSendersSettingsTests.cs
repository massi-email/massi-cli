namespace EmailSweepAutomation.Test.UnitTests;

using EmailSweepAutomation.Commands;
using Xunit;

public class TopSendersSettingsTests
{
    [Fact]
    public void Validate_InteractiveWithoutRulesFile_Fails()
    {
        var settings = new TopSendersCommand.Settings
        {
            Email = "a@example.com",
            Interactive = true,
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("--interactive", result.Message);
    }

    [Fact]
    public void Validate_InteractiveWithRulesFile_Succeeds()
    {
        var settings = new TopSendersCommand.Settings
        {
            Email = "a@example.com",
            Interactive = true,
            RulesFile = "rules.json",
        };

        var result = settings.Validate();

        Assert.True(result.Successful);
    }

    [Fact]
    public void Validate_NonInteractive_SucceedsWithoutRulesFile()
    {
        var settings = new TopSendersCommand.Settings
        {
            Email = "a@example.com",
        };

        var result = settings.Validate();

        Assert.True(result.Successful);
    }

    [Fact]
    public void Validate_NonInteractiveWithRulesFile_Succeeds()
    {
        var settings = new TopSendersCommand.Settings
        {
            Email = "a@example.com",
            RulesFile = "rules.json",
        };

        var result = settings.Validate();

        Assert.True(result.Successful);
    }
}
