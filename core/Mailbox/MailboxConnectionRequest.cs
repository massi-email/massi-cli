namespace EmailSweepAutomation;

/// <summary>
/// Fully-resolved instructions for connecting to a mailbox. <see cref="Provider"/> is the
/// connector key ("gmail", "imap", "m365"). <see cref="Username"/>/<see cref="Password"/>
/// drive the basic-auth IMAP path; <see cref="AccessToken"/> drives OAuth (XOAUTH2) paths.
/// </summary>
public sealed record MailboxConnectionRequest(
    string Provider,
    string Email,
    string? Host,
    int? Port,
    bool UseSsl,
    string? Username,
    string? Password)
{
    /// <summary>OAuth2 bearer access token for XOAUTH2 connectors (resolved/refreshed by the host).</summary>
    public string? AccessToken { get; init; }
}
