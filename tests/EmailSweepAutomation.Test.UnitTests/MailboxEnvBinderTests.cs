namespace EmailSweepAutomation.Test.UnitTests;

using System;
using System.Collections.Generic;
using EmailSweepAutomation;
using EmailSweepAutomation.Commands;
using Xunit;

public class MailboxEnvBinderTests
{
    private static Func<string, string?> Env(params (string Key, string? Value)[] pairs)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
            map[key] = value;
        return name => map.TryGetValue(name, out var v) ? v : null;
    }

    [Fact]
    public void Apply_FillsHostFromEnv_WhenFlagUnset()
    {
        var s = new MailboxCommandSettings { Provider = "imap" };
        var err = MailboxEnvBinder.Apply(s, Env((MailboxEnvBinder.HostEnv, "imap.x.com")));
        Assert.Null(err);
        Assert.Equal("imap.x.com", s.Host);
    }

    [Fact]
    public void Apply_HostFlagWins_OverEnv()
    {
        var s = new MailboxCommandSettings { Provider = "imap", Host = "flag.com" };
        MailboxEnvBinder.Apply(s, Env((MailboxEnvBinder.HostEnv, "env.com")));
        Assert.Equal("flag.com", s.Host);
    }

    [Fact]
    public void Apply_FillsUserFromEnv_WhenFlagUnset()
    {
        var s = new MailboxCommandSettings { Provider = "imap" };
        MailboxEnvBinder.Apply(s, Env((MailboxEnvBinder.UserEnv, "login")));
        Assert.Equal("login", s.User);
    }

    [Fact]
    public void Apply_UserFlagWins_OverEnv()
    {
        var s = new MailboxCommandSettings { Provider = "imap", User = "flaguser" };
        MailboxEnvBinder.Apply(s, Env((MailboxEnvBinder.UserEnv, "envuser")));
        Assert.Equal("flaguser", s.User);
    }

    [Fact]
    public void Apply_FillsProviderFromEnv_WhenFlagUnset()
    {
        var s = new MailboxCommandSettings { Provider = "" };
        MailboxEnvBinder.Apply(s, Env((MailboxEnvBinder.ProviderEnv, "fastmail")));
        Assert.Equal("fastmail", s.Provider);
    }

    [Fact]
    public void Apply_ProviderFlagWins_OverEnv()
    {
        var s = new MailboxCommandSettings { Provider = "imap" };
        MailboxEnvBinder.Apply(s, Env((MailboxEnvBinder.ProviderEnv, "fastmail")));
        Assert.Equal("imap", s.Provider);
    }

    [Fact]
    public void Apply_ProviderDefaultsToGmail_WhenNeitherSet()
    {
        var s = new MailboxCommandSettings { Provider = "" };
        MailboxEnvBinder.Apply(s, Env());
        Assert.Equal("gmail", s.Provider);
    }

    [Fact]
    public void Apply_FillsPortFromEnv_WhenFlagUnset()
    {
        var s = new MailboxCommandSettings { Provider = "imap" };
        var err = MailboxEnvBinder.Apply(s, Env((MailboxEnvBinder.PortEnv, "143")));
        Assert.Null(err);
        Assert.Equal(143, s.Port);
    }

    [Fact]
    public void Apply_PortFlagWins_OverEnv()
    {
        var s = new MailboxCommandSettings { Provider = "imap", Port = 993 };
        MailboxEnvBinder.Apply(s, Env((MailboxEnvBinder.PortEnv, "143")));
        Assert.Equal(993, s.Port);
    }

    [Fact]
    public void Apply_InvalidPortEnv_ReturnsError()
    {
        var s = new MailboxCommandSettings { Provider = "imap" };
        var err = MailboxEnvBinder.Apply(s, Env((MailboxEnvBinder.PortEnv, "notaport")));
        Assert.NotNull(err);
        Assert.Contains("MASSI_IMAP_PORT", err);
        Assert.Contains("notaport", err);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("70000")]
    [InlineData("-1")]
    public void Apply_OutOfRangePortEnv_ReturnsError(string raw)
    {
        var s = new MailboxCommandSettings { Provider = "imap" };
        var err = MailboxEnvBinder.Apply(s, Env((MailboxEnvBinder.PortEnv, raw)));
        Assert.NotNull(err);
    }

    [Fact]
    public void Apply_BlankEnv_TreatedAsAbsent()
    {
        var s = new MailboxCommandSettings { Provider = "imap" };
        MailboxEnvBinder.Apply(s, Env((MailboxEnvBinder.HostEnv, "   ")));
        Assert.Null(s.Host);
    }

    [Fact]
    public void Apply_TrimsBoundValues()
    {
        var s = new MailboxCommandSettings { Provider = "imap" };
        MailboxEnvBinder.Apply(s, Env((MailboxEnvBinder.HostEnv, "  imap.x.com  ")));
        Assert.Equal("imap.x.com", s.Host);
    }

    [Fact]
    public void Apply_BlankPortEnv_TreatedAsAbsent()
    {
        var s = new MailboxCommandSettings { Provider = "imap" };
        var err = MailboxEnvBinder.Apply(s, Env((MailboxEnvBinder.PortEnv, "  ")));
        Assert.Null(err);
        Assert.Null(s.Port);
    }

    [Fact]
    public void Apply_ReadsExpectedEnvVarNames()
    {
        var captured = new List<string>();
        Func<string, string?> capture = name => { captured.Add(name); return null; };

        var s = new MailboxCommandSettings { Provider = "", Host = null, User = null, Port = null };
        MailboxEnvBinder.Apply(s, capture);

        Assert.Contains("MASSI_IMAP_PROVIDER", captured);
        Assert.Contains("MASSI_IMAP_HOST", captured);
        Assert.Contains("MASSI_IMAP_PORT", captured);
        Assert.Contains("MASSI_IMAP_USER", captured);
    }
}
