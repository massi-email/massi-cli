using EmailSweepAutomation.Commands;

namespace EmailSweepAutomation;

/// <summary>
/// Pure mapping from command settings to a <see cref="MailboxConnectionRequest"/>, plus its
/// validation companion. The preset table is the only thing that grows when adding a
/// basic-auth provider; OAuth providers add a connector instead.
/// </summary>
public static class MailboxConnectionResolver
{
    private sealed record Preset(string Connector, string? Host, int? Port, bool UseSsl);

    private static readonly IReadOnlyDictionary<string, Preset> Presets =
        new Dictionary<string, Preset>(StringComparer.OrdinalIgnoreCase)
        {
            ["gmail"]    = new Preset("gmail", null, null, true),
            ["imap"]     = new Preset("imap", null, 993, true),
            ["apple"]    = new Preset("imap", "imap.mail.me.com", 993, true),
            ["yahoo"]    = new Preset("imap", "imap.mail.yahoo.com", 993, true),
            ["fastmail"] = new Preset("imap", "imap.fastmail.com", 993, true),
        };

    private const string KnownProviders = "gmail, imap, apple, yahoo, fastmail";

    /// <summary>Returns an error message, or null when the settings are valid.</summary>
    public static string? Validate(MailboxCommandSettings s)
    {
        if (!Presets.TryGetValue(s.Provider, out var preset))
            return $"Unknown provider '{s.Provider}'. Known: {KnownProviders}.";

        if (preset.Connector == "gmail")
            return string.IsNullOrWhiteSpace(s.Email) ? "Email not specified" : null;

        // basic-auth imap connector
        var host = string.IsNullOrWhiteSpace(s.Host) ? preset.Host : s.Host;
        if (string.IsNullOrWhiteSpace(host))
            return "--host is required with --provider imap";
        if (string.IsNullOrWhiteSpace(s.User) && string.IsNullOrWhiteSpace(s.Email))
            return $"--user (or the Email argument) is required with --provider {s.Provider}";
        return null;
    }

    /// <summary>Resolves settings (assumed already validated) into a connection request.</summary>
    public static MailboxConnectionRequest Resolve(MailboxCommandSettings s, string? password)
    {
        var preset = Presets[s.Provider];
        var host = string.IsNullOrWhiteSpace(s.Host) ? preset.Host : s.Host;
        var port = s.Port ?? preset.Port;
        var useSsl = preset.UseSsl && !s.StartTls;
        var username = string.IsNullOrWhiteSpace(s.User) ? s.Email : s.User;
        return new MailboxConnectionRequest(preset.Connector, s.Email, host, port, useSsl, username, password);
    }
}
