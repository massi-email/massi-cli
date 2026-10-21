namespace EmailSweepAutomation.Test.UnitTests;

using System;
using EmailSweepAutomation.Commands;
using Xunit;

public class MailboxCommandSettingsTests
{
    [Fact]
    public void Validate_ValidGmail_Succeeds()
    {
        var s = new MailboxCommandSettings { Provider = "gmail", Email = "a@b.com" };
        Assert.True(s.Validate().Successful);
    }

    [Fact]
    public void Validate_ValidImap_Succeeds()
    {
        var s = new MailboxCommandSettings { Provider = "imap", Host = "imap.x.com", Email = "a@b.com" };
        Assert.True(s.Validate().Successful);
    }

    [Fact]
    public void Validate_UnknownProvider_Fails()
    {
        var s = new MailboxCommandSettings { Provider = "nope", Email = "a@b.com" };
        var result = s.Validate();
        Assert.False(result.Successful);
        Assert.Contains("Unknown provider", result.Message);
    }

    [Fact]
    public void Validate_InvalidPortEnvVar_FailsViaBinder()
    {
        Environment.SetEnvironmentVariable("MASSI_IMAP_PORT", "notaport");
        try
        {
            var s = new MailboxCommandSettings { Provider = "imap", Host = "imap.x.com", Email = "a@b.com" };
            var result = s.Validate();
            Assert.False(result.Successful);
            Assert.Contains("MASSI_IMAP_PORT", result.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MASSI_IMAP_PORT", null);
        }
    }

    [Fact]
    public void Validate_HostFromEnvVar_BindsViaValidate()
    {
        Environment.SetEnvironmentVariable("MASSI_IMAP_HOST", "imap.from-env.com");
        try
        {
            // No --host flag; provider imap requires a host, so success here proves the
            // binder ran inside Validate() and filled s.Host from the env var.
            var s = new MailboxCommandSettings { Provider = "imap", Email = "a@b.com" };
            var result = s.Validate();
            Assert.True(result.Successful);
            Assert.Equal("imap.from-env.com", s.Host);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MASSI_IMAP_HOST", null);
        }
    }
}
