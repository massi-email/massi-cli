namespace EmailSweepAutomation.Test.UnitTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using EmailSweepAutomation;
using MailKit.Net.Imap;
using Xunit;

public class MailClientFactoryTests
{
    private sealed class RecordingConnector : IMailConnector
    {
        public RecordingConnector(string provider) => Provider = provider;
        public string Provider { get; }
        public bool WasCalled { get; private set; }
        public Task<ImapClient> ConnectAsync(MailboxConnectionRequest req, CancellationToken ct)
        {
            WasCalled = true;
            // These tests assert dispatch only; no real client is needed.
            return Task.FromResult<ImapClient>(null!);
        }
    }

    private static MailboxConnectionRequest Request(string provider) =>
        new(provider, "a@b.com", "h", 993, true, "a@b.com", null);

    [Fact]
    public async Task ConnectAsync_DispatchesToMatchingConnector()
    {
        var gmail = new RecordingConnector("gmail");
        var imap = new RecordingConnector("imap");
        var factory = new MailClientFactory(new IMailConnector[] { gmail, imap });

        await factory.ConnectAsync(Request("imap"), CancellationToken.None);

        Assert.True(imap.WasCalled);
        Assert.False(gmail.WasCalled);
    }

    [Fact]
    public async Task ConnectAsync_IsCaseInsensitiveOnProvider()
    {
        var gmail = new RecordingConnector("gmail");
        var factory = new MailClientFactory(new IMailConnector[] { gmail });

        await factory.ConnectAsync(Request("GMAIL"), CancellationToken.None);

        Assert.True(gmail.WasCalled);
    }

    [Fact]
    public async Task ConnectAsync_UnknownProvider_Throws()
    {
        var factory = new MailClientFactory(new IMailConnector[] { new RecordingConnector("gmail") });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.ConnectAsync(Request("imap"), CancellationToken.None));
    }
}
