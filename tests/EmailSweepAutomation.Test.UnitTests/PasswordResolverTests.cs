namespace EmailSweepAutomation.Test.UnitTests;

using System;
using EmailSweepAutomation;
using Xunit;

public class PasswordResolverTests
{
    [Fact]
    public void Resolve_FlagWins()
    {
        var result = PasswordResolver.Resolve("flagpw", _ => "envpw", () => "prompted");
        Assert.Equal("flagpw", result);
    }

    [Fact]
    public void Resolve_EmptyFlag_FallsBackToEnv()
    {
        var result = PasswordResolver.Resolve("", _ => "envpw", () => "prompted");
        Assert.Equal("envpw", result);
    }

    [Fact]
    public void Resolve_NoFlag_UsesEnv()
    {
        var result = PasswordResolver.Resolve(null, _ => "envpw", () => "prompted");
        Assert.Equal("envpw", result);
    }

    [Fact]
    public void Resolve_NoFlagNoEnv_Prompts()
    {
        var result = PasswordResolver.Resolve(null, _ => null, () => "prompted");
        Assert.Equal("prompted", result);
    }

    [Fact]
    public void Resolve_EmptyEnv_Prompts()
    {
        var result = PasswordResolver.Resolve(null, _ => "", () => "prompted");
        Assert.Equal("prompted", result);
    }

    [Fact]
    public void Resolve_ReadsTheExpectedEnvVarName()
    {
        string? captured = null;
        PasswordResolver.Resolve(null, name => { captured = name; return "x"; }, () => "prompted");
        Assert.Equal("MASSI_IMAP_PASSWORD", captured);
    }
}
