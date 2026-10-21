using MailKit.Net.Imap;

namespace EmailSweepAutomation;

/// <summary>Resolves a <see cref="MailboxConnectionRequest"/> to the matching connector.</summary>
public interface IMailClientFactory
{
    Task<ImapClient> ConnectAsync(MailboxConnectionRequest req, CancellationToken ct);
}
