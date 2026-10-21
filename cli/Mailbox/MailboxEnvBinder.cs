using EmailSweepAutomation.Commands;

namespace EmailSweepAutomation;

/// <summary>
/// Fills unset mailbox connection flags from MASSI_IMAP_* environment variables, so a custom
/// IMAP mailbox can be configured once (env or .env) instead of repeated on every command.
/// An explicit flag always wins over the env var. The env lookup is injected for testability,
/// mirroring <see cref="PasswordResolver"/>.
/// </summary>
public static class MailboxEnvBinder
{
    public const string ProviderEnv = "MASSI_IMAP_PROVIDER";
    public const string HostEnv = "MASSI_IMAP_HOST";
    public const string PortEnv = "MASSI_IMAP_PORT";
    public const string UserEnv = "MASSI_IMAP_USER";

    /// <summary>
    /// Mutates <paramref name="s"/>, filling only the flags the user left unset from the
    /// environment. Returns an error message when an env value is malformed, otherwise null.
    /// Owns the <c>gmail</c> provider default so an unset provider can fall through to the env var.
    /// </summary>
    public static string? Apply(MailboxCommandSettings s, Func<string, string?> env)
    {
        if (string.IsNullOrWhiteSpace(s.Provider))
        {
            var provider = env(ProviderEnv);
            s.Provider = string.IsNullOrWhiteSpace(provider) ? "gmail" : provider.Trim();
        }

        if (string.IsNullOrWhiteSpace(s.Host))
        {
            var host = env(HostEnv);
            if (!string.IsNullOrWhiteSpace(host))
                s.Host = host.Trim();
        }

        if (string.IsNullOrWhiteSpace(s.User))
        {
            var user = env(UserEnv);
            if (!string.IsNullOrWhiteSpace(user))
                s.User = user.Trim();
        }

        if (s.Port is null)
        {
            var rawPort = env(PortEnv);
            if (!string.IsNullOrWhiteSpace(rawPort))
            {
                if (int.TryParse(rawPort.Trim(), out var port) && port is > 0 and <= 65535)
                    s.Port = port;
                else
                    return $"{PortEnv} is not a valid port number: '{rawPort}'.";
            }
        }

        return null;
    }
}
