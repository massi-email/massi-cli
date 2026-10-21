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

    /// <summary>
    /// Connect with no encryption at all, rather than requiring STARTTLS.
    /// </summary>
    /// <remarks>
    /// Off unless a caller sets it, and the asymmetry is the point. Without <see cref="UseSsl"/>
    /// the connector requires STARTTLS and fails loudly if the server will not do it — because
    /// the alternative, MailKit's "upgrade if the server offers it", puts the password on the
    /// wire in cleartext against a server that does not, and reports success either way. Nobody
    /// can tell afterwards which of the two happened.
    ///
    /// This flag is how a caller says the plain connection is what it actually wants: a mail
    /// server on the same machine or the same LAN, where there is no network to eavesdrop on.
    /// It has to be asked for by name, once, by something that knows where the server is — never
    /// inferred from a failed handshake.
    /// </remarks>
    public bool AllowCleartext { get; init; }
}
