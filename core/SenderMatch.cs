namespace EmailSweepAutomation;

/// <summary>
/// Sender matching for domains and branch entries. Plain string logic on purpose — no public
/// suffix list in the engine: <c>shop.com</c> matches <c>shop.com</c> and any subdomain of it,
/// and nothing that merely ends in the same letters.
/// </summary>
public static class SenderMatch
{
    /// <summary>The host part of an address, or null when there is none.</summary>
    public static string? Host(string address)
    {
        var at = address.LastIndexOf('@');
        return at < 0 || at == address.Length - 1 ? null : address[(at + 1)..];
    }

    /// <summary>True when the address's host is <paramref name="domain"/> or a subdomain of it.</summary>
    public static bool DomainMatches(string address, string domain)
    {
        var host = Host(address);
        return host is not null
            && (host.Equals(domain, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Whether the address is reached by the entry, as <see cref="SenderEntry"/> reads it. An
    /// entry that is no valid entry still matches the way the engine always read it, so a rule
    /// stored before validation does not change what it sweeps.
    /// </summary>
    public static bool EntryMatches(string address, string entry) => SenderEntry.Read(entry).Matches(address);

    /// <summary>
    /// Whether the entry is a well-formed name pattern, as opposed to an exact address, a host,
    /// or something that is no valid entry at all.
    /// </summary>
    public static bool IsPattern(string entry) => SenderEntry.Parse(entry) is NamePattern;

    public static bool IsValidHost(string? host) =>
        !string.IsNullOrEmpty(host)
        && host.Contains('.')
        && !host.StartsWith('.') && !host.EndsWith('.')
        && !host.Any(c => char.IsWhiteSpace(c) || c is '@' or '*');

    public static bool IsValidEntry(string? entry) => SenderEntry.Parse(entry) is not null;
}
