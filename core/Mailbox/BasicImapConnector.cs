using MailKit.Net.Imap;
using MailKit.Security;

namespace EmailSweepAutomation;

/// <summary>Basic-auth IMAP connector for any host (used by imap/apple/yahoo/fastmail).</summary>
public sealed class BasicImapConnector : IMailConnector
{
    /// <summary>Implicit TLS from the first byte (IMAPS).</summary>
    public const int ImapsPort = 993;

    /// <summary>Cleartext IMAP, upgraded in-band by STARTTLS.</summary>
    public const int ImapPort = 143;

    public string Provider => "imap";

    /// <summary>
    /// How the connection is secured.
    /// </summary>
    /// <remarks>
    /// The no-SSL branch is <see cref="SecureSocketOptions.StartTls"/>, not
    /// <c>StartTlsWhenAvailable</c>. The difference is the whole point: "when available" means
    /// MailKit sends the password in cleartext against any server that does not advertise
    /// STARTTLS — including one that stripped the advertisement — and reports success. A user
    /// who unticks "Use SSL" is choosing a port and an upgrade, not choosing to broadcast their
    /// password, and there is no way for them to tell afterwards which of the two happened.
    /// <see cref="SecureSocketOptions.StartTls"/> throws instead, which is a failure the user
    /// can see and act on.
    ///
    /// <paramref name="allowCleartext"/> is the one way to get the plain connection, and it has
    /// to be asked for: a mail server on the same machine or LAN is a real case, and the
    /// difference that matters is between a caller that chose this and a handshake that quietly
    /// gave up.
    /// </remarks>
    public static SecureSocketOptions SecureOptionsFor(bool useSsl, bool allowCleartext = false) =>
        useSsl ? SecureSocketOptions.SslOnConnect
        : allowCleartext ? SecureSocketOptions.None
        : SecureSocketOptions.StartTls;

    /// <summary>
    /// The port to connect on when the request names none. It has to follow the security
    /// setting: 993 speaks TLS from the first byte, so a STARTTLS connection to it reads the
    /// handshake as a protocol greeting and fails in a way that names neither.
    /// </summary>
    public static int PortFor(int? port, bool useSsl) => port ?? (useSsl ? ImapsPort : ImapPort);

    public async Task<ImapClient> ConnectAsync(MailboxConnectionRequest req, CancellationToken ct)
    {
        var client = new ImapClient();
        try
        {
            await client.ConnectAsync(
                req.Host,
                PortFor(req.Port, req.UseSsl),
                SecureOptionsFor(req.UseSsl, req.AllowCleartext),
                ct);
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
