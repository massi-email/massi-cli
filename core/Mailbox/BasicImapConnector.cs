using MailKit.Net.Imap;
using MailKit.Security;

namespace EmailSweepAutomation;

/// <summary>Basic-auth IMAP connector for any host (used by imap/apple/yahoo/fastmail).</summary>
public sealed class BasicImapConnector : IMailConnector
{
    public string Provider => "imap";

    public async Task<ImapClient> ConnectAsync(MailboxConnectionRequest req, CancellationToken ct)
    {
        var client = new ImapClient();
        try
        {
            var secure = req.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(req.Host, req.Port ?? 993, secure, ct);
            await client.AuthenticateAsync(req.Username, req.Password, ct);
            return client;
        }
        catch
        {
            // Don't leak the open connection if authentication (or connect) fails.
            client.Dispose();
            throw;
        }
    }
}
