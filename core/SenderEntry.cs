namespace EmailSweepAutomation;

/// <summary>
/// A branch sender entry, read once: <c>@*.host</c> is the host and its subdomains, <c>@host</c>
/// exactly that host, a <c>*</c> at either end of the name a pattern on that exact host
/// (<c>promo*@host</c> starts with, <c>*promo@host</c> ends with, <c>*promo*@host</c>
/// contains), anything else an exact address. All matching is case-insensitive.
/// </summary>
public abstract record SenderEntry
{
    /// <summary>The entry, or null when it is no valid entry.</summary>
    public static SenderEntry? Parse(string? entry) =>
        !string.IsNullOrWhiteSpace(entry) && Read(entry) is var e && e.IsValid ? e : null;

    /// <summary>
    /// How the engine reads an entry, valid or not. Rules stored before an entry was validated
    /// match the way they always did: the shape is taken from the prefix and the stars alone,
    /// and whatever fits no other shape is compared as a whole string.
    /// </summary>
    internal static SenderEntry Read(string entry)
    {
        if (entry.StartsWith("@*.", StringComparison.Ordinal)) return new HostAndSubdomains(entry[3..]);
        if (entry.StartsWith('@')) return new ExactHost(entry[1..]);
        return NamePattern.Shape(entry) ?? (SenderEntry)new ExactAddress(entry);
    }

    public abstract bool Matches(string address);

    private protected abstract bool IsValid { get; }

    /// <summary>A name holds neither whitespace nor an <c>@</c>.</summary>
    private protected static bool IsValidName(string name) => !name.Any(c => char.IsWhiteSpace(c) || c == '@');
}

/// <summary><c>@*.host</c>: the host and any subdomain of it.</summary>
public sealed record HostAndSubdomains(string Host) : SenderEntry
{
    public override bool Matches(string address) => SenderMatch.DomainMatches(address, Host);
    private protected override bool IsValid => SenderMatch.IsValidHost(Host);
}

/// <summary><c>@host</c>: exactly that host.</summary>
public sealed record ExactHost(string Host) : SenderEntry
{
    public override bool Matches(string address) =>
        string.Equals(SenderMatch.Host(address), Host, StringComparison.OrdinalIgnoreCase);
    private protected override bool IsValid => SenderMatch.IsValidHost(Host);
}

/// <summary><c>name@host</c>: that one address.</summary>
public sealed record ExactAddress(string Address) : SenderEntry
{
    public override bool Matches(string address) => string.Equals(address, Address, StringComparison.OrdinalIgnoreCase);

    // A star anywhere but the ends of the name would read as a pattern and match as an exact address.
    private protected override bool IsValid
    {
        get
        {
            var at = Address.LastIndexOf('@');
            return at > 0 && IsValidName(Address[..at]) && !Address[..at].Contains('*')
                && SenderMatch.IsValidHost(Address[(at + 1)..]);
        }
    }
}

/// <summary>
/// A star at the start and/or the end of the name, on one exact host, with a literal between
/// that holds no star of its own.
/// </summary>
public sealed record NamePattern(string Literal, bool OpenStart, bool OpenEnd, string Host) : SenderEntry
{
    public override bool Matches(string address)
    {
        var at = address.LastIndexOf('@');
        if (at <= 0 || !string.Equals(address[(at + 1)..], Host, StringComparison.OrdinalIgnoreCase)) return false;
        var local = address[..at];
        return (OpenStart, OpenEnd) switch
        {
            (true, true) => local.Contains(Literal, StringComparison.OrdinalIgnoreCase),
            (true, false) => local.EndsWith(Literal, StringComparison.OrdinalIgnoreCase),
            _ => local.StartsWith(Literal, StringComparison.OrdinalIgnoreCase),
        };
    }

    private protected override bool IsValid => IsValidName(Literal) && SenderMatch.IsValidHost(Host);

    /// <summary>The pattern an entry is shaped as, or null: <c>*@host</c> and <c>**@host</c> have no literal.</summary>
    internal static NamePattern? Shape(string entry)
    {
        var at = entry.LastIndexOf('@');
        if (at <= 0) return null;
        var local = entry[..at];
        var openStart = local.StartsWith('*');
        var openEnd = local.Length > 1 && local.EndsWith('*');
        if (!openStart && !openEnd) return null;
        var literal = local[(openStart ? 1 : 0)..(openEnd ? ^1 : local.Length)];
        return literal.Length == 0 || literal.Contains('*') ? null : new NamePattern(literal, openStart, openEnd, entry[(at + 1)..]);
    }
}
