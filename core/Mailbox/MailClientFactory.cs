using MailKit.Net.Imap;

namespace EmailSweepAutomation;

public sealed class MailClientFactory : IMailClientFactory
{
    private readonly IReadOnlyDictionary<string, IMailConnector> connectors;

    public MailClientFactory(IEnumerable<IMailConnector> connectors)
        => this.connectors = connectors.ToDictionary(c => c.Provider, c => c, StringComparer.OrdinalIgnoreCase);

    public Task<ImapClient> ConnectAsync(MailboxConnectionRequest req, CancellationToken ct)
        => connectors.TryGetValue(req.Provider, out var connector)
            ? connector.ConnectAsync(req, ct)
            : throw new InvalidOperationException(
                $"No connector for provider '{req.Provider}'. Known: {string.Join(", ", connectors.Keys)}.");
}
