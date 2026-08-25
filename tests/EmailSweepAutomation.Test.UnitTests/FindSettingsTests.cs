namespace EmailSweepAutomation.Test.UnitTests;

using EmailSweepAutomation.Commands;
using Xunit;

public class FindSettingsTests
{
    [Fact]
    public void Validate_WithoutFromOrSubject_Fails()
    {
        var settings = new FindCommand.Settings
        {
            Email = "a@example.com",
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("--from or --subject", result.Message);
    }

    [Fact]
    public void Validate_WithFrom_Succeeds()
    {
        var settings = new FindCommand.Settings
        {
            Email = "a@example.com",
            From = "boss@company.com",
        };

        var result = settings.Validate();

        Assert.True(result.Successful);
    }

    [Fact]
    public void Validate_WithSubject_Succeeds()
    {
        var settings = new FindCommand.Settings
        {
            Email = "a@example.com",
            Subject = "invoice",
        };

        var result = settings.Validate();

        Assert.True(result.Successful);
    }

    [Fact]
    public void Validate_WithBothCountAndSince_Fails()
    {
        var settings = new FindCommand.Settings
        {
            Email = "a@example.com",
            From = "boss@company.com",
            Count = 100,
            Since = "30d",
        };

        var result = settings.Validate();

        Assert.False(result.Successful);
    }

    [Fact]
    public void ToTransientRule_BuildsValidAlertRule()
    {
        var settings = new FindCommand.Settings
        {
            Email = "a@example.com",
            From = "boss@company.com",
            Subject = "  ",
        };

        var rule = settings.ToTransientRule();

        Assert.True(rule.Alert);
        Assert.Equal("boss@company.com", rule.FromAddress);
        Assert.Null(rule.SubjectContains); // whitespace-only flag is treated as absent
        var (isValid, error) = rule.IsRuleValid();
        Assert.True(isValid);
        Assert.Null(error);
    }
}
