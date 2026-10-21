namespace EmailSweepAutomation.Test.UnitTests;

using EmailSweepAutomation;
using EmailSweepAutomation.Commands;
using Xunit;

public class MailboxConnectionResolverTests
{
    [Fact]
    public void Validate_GmailDefault_WithEmail_Succeeds()
    {
        var s = new MailboxCommandSettings { Provider = "gmail", Email = "a@b.com" };
        Assert.Null(MailboxConnectionResolver.Validate(s));
    }

    [Fact]
    public void Validate_Gmail_WithoutEmail_Fails()
    {
        var s = new MailboxCommandSettings { Provider = "gmail", Email = "" };
        Assert.Contains("Email", MailboxConnectionResolver.Validate(s));
    }

    [Fact]
    public void Validate_ImapWithoutHost_Fails()
    {
        var s = new MailboxCommandSettings { Provider = "imap", Email = "a@b.com" };
        Assert.Contains("--host", MailboxConnectionResolver.Validate(s));
    }

    [Fact]
    public void Validate_ImapWithoutUserOrEmail_Fails()
    {
        var s = new MailboxCommandSettings { Provider = "imap", Host = "imap.x.com" };
        Assert.Contains("--user", MailboxConnectionResolver.Validate(s));
    }

    [Fact]
    public void Validate_UnknownProvider_Fails()
    {
        var s = new MailboxCommandSettings { Provider = "weird", Email = "a@b.com" };
        Assert.Contains("Unknown provider", MailboxConnectionResolver.Validate(s));
    }

    [Fact]
    public void Resolve_Gmail_SetsGmailConnector()
    {
        var s = new MailboxCommandSettings { Provider = "gmail", Email = "a@b.com" };
        var r = MailboxConnectionResolver.Resolve(s, null);
        Assert.Equal("gmail", r.Provider);
        Assert.Equal("a@b.com", r.Email);
    }

    [Fact]
    public void Resolve_Imap_WithExplicitHost_FillsDefaults()
    {
        var s = new MailboxCommandSettings { Provider = "imap", Host = "imap.x.com", Email = "a@b.com" };
        var r = MailboxConnectionResolver.Resolve(s, null);
        Assert.Equal("imap", r.Provider);
        Assert.Equal("imap.x.com", r.Host);
        Assert.Equal(993, r.Port);
        Assert.True(r.UseSsl);
        Assert.Equal("a@b.com", r.Username);
    }

    [Fact]
    public void Resolve_ApplePreset_FillsHost()
    {
        var s = new MailboxCommandSettings { Provider = "apple", Email = "a@me.com" };
        var r = MailboxConnectionResolver.Resolve(s, null);
        Assert.Equal("imap", r.Provider);
        Assert.Equal("imap.mail.me.com", r.Host);
        Assert.Equal(993, r.Port);
    }

    [Fact]
    public void Resolve_ExplicitHost_OverridesPreset()
    {
        var s = new MailboxCommandSettings { Provider = "apple", Host = "custom.com", Email = "a@me.com" };
        var r = MailboxConnectionResolver.Resolve(s, null);
        Assert.Equal("custom.com", r.Host);
    }

    [Fact]
    public void Resolve_ExplicitPort_OverridesPreset()
    {
        var s = new MailboxCommandSettings { Provider = "imap", Host = "h", Port = 143, Email = "a@b.com" };
        var r = MailboxConnectionResolver.Resolve(s, null);
        Assert.Equal(143, r.Port);
    }

    [Fact]
    public void Resolve_StartTls_DisablesSsl()
    {
        var s = new MailboxCommandSettings { Provider = "imap", Host = "h", StartTls = true, Email = "a@b.com" };
        var r = MailboxConnectionResolver.Resolve(s, null);
        Assert.False(r.UseSsl);
    }

    [Fact]
    public void Resolve_User_OverridesEmailAsUsername()
    {
        var s = new MailboxCommandSettings { Provider = "imap", Host = "h", User = "login", Email = "a@b.com" };
        var r = MailboxConnectionResolver.Resolve(s, null);
        Assert.Equal("login", r.Username);
    }

    [Fact]
    public void Resolve_Password_IsPassedThrough()
    {
        var s = new MailboxCommandSettings { Provider = "imap", Host = "h", Email = "a@b.com" };
        var r = MailboxConnectionResolver.Resolve(s, "secret");
        Assert.Equal("secret", r.Password);
    }

    [Fact]
    public void Resolve_ProviderIsCaseInsensitive()
    {
        var s = new MailboxCommandSettings { Provider = "APPLE", Email = "a@me.com" };
        Assert.Null(MailboxConnectionResolver.Validate(s));
        var r = MailboxConnectionResolver.Resolve(s, null);
        Assert.Equal("imap", r.Provider);
        Assert.Equal("imap.mail.me.com", r.Host);
    }
}
