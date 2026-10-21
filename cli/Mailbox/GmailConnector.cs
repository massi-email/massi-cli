using EmailSweepAutomation.Configuration;
using MailKit.Net.Imap;
using Microsoft.Extensions.Options;

namespace EmailSweepAutomation;

/// <summary>OAuth connector that delegates to the existing <see cref="GmailService"/>.</summary>
public sealed class GmailConnector : IMailConnector
{
    private readonly GmailService gmailService;
    private readonly GmailSettingsOptions gmailSettings;

    public GmailConnector(GmailService gmailService, IOptions<GmailSettingsOptions> options)
    {
        this.gmailService = gmailService ?? throw new ArgumentNullException(nameof(gmailService));
        this.gmailSettings = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string Provider => "gmail";

    public Task<ImapClient> ConnectAsync(MailboxConnectionRequest req, CancellationToken ct)
        => gmailService.GetClientAsync(req.Email, gmailSettings.ClientId, gmailSettings.ClientSecret);
}
