using MailKit.Net.Imap;

namespace EmailSweepAutomation;

/// <summary>
/// Produces a connected, authenticated <see cref="ImapClient"/> for one auth mechanism.
/// Implementations are registered as a set and dispatched by <see cref="Provider"/>.
/// </summary>
public interface IMailConnector
{
    string Provider { get; }
    Task<ImapClient> ConnectAsync(MailboxConnectionRequest req, CancellationToken ct);
}
