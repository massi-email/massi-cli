using MailKit;
using MailKit.Net.Imap;

namespace EmailSweepAutomation;

/// <summary>
/// Finds the mailbox's Trash folder, which is what decides whether a sweep's deletes are
/// recoverable: <see cref="IRuleEngine.DeleteMessagesAsync"/> moves messages there when one
/// exists and flags-plus-expunges — irreversibly — when one does not.
/// </summary>
/// <remarks>
/// Asking <c>ImapClient.GetFolder(SpecialFolder.Trash)</c> and treating anything else as "no
/// Trash" is wrong in both of its failure modes, and both fail in the destructive direction:
/// <list type="bullet">
/// <item>the server advertises SPECIAL-USE or XLIST but flags no trash mailbox — MailKit
/// returns <see langword="null"/>;</item>
/// <item>the server advertises neither extension — MailKit throws
/// <see cref="NotSupportedException"/>.</item>
/// </list>
/// RFC 6154 is an extension, not a requirement, so a perfectly ordinary server with a folder
/// literally called <c>Trash</c> lands in the second case. Every message a sweep deleted on
/// such a server was expunged permanently.
///
/// So the special-use answer is taken when there is one, and otherwise the personal namespaces
/// are listed and searched — first for the <see cref="FolderAttributes.Trash"/> attribute (some
/// servers set it on a folder they never advertise), then by name.
/// </remarks>
public static class TrashFolderResolver
{
    /// <summary>
    /// Folder names that mean "trash", in the order they are preferred. Case-insensitive: IMAP
    /// folder names are case-sensitive to the protocol, but no server in service distinguishes
    /// <c>Trash</c> from <c>TRASH</c> in a way a user could mean.
    /// </summary>
    private static readonly string[] TrashNames =
        ["Trash", "Deleted Items", "Deleted Messages", "Deleted", "Bin"];

    /// <summary>Gmail's IMAP hierarchy hides Trash under a container folder.</summary>
    private const string GmailTrashPath = "[Gmail]/Trash";

    /// <summary>
    /// The trash folder, or null when this mailbox genuinely has none.
    /// </summary>
    /// <param name="capabilities">What the server advertised, which decides whether asking for
    /// a special folder is meaningful at all.</param>
    /// <param name="special">
    /// The special-folder lookup — <c>client.GetFolder</c> in production. A seam, because
    /// <see cref="ImapClient"/> is sealed and this decision is the one worth testing.
    /// </param>
    /// <param name="personalFolders">
    /// Every folder in the account's personal namespaces. Only enumerated when the special-use
    /// route produced nothing, so <see cref="ResolveAsync"/> does not list a mailbox it need not.
    /// </param>
    public static IMailFolder? Resolve(
        ImapCapabilities capabilities,
        Func<SpecialFolder, IMailFolder?> special,
        IEnumerable<IMailFolder> personalFolders) =>
        TrySpecial(capabilities, special) ?? Scan(personalFolders);

    /// <summary>
    /// <see cref="Resolve"/> against a connected client, listing the personal namespaces only
    /// if it has to.
    /// </summary>
    public static async Task<IMailFolder?> ResolveAsync(ImapClient client, CancellationToken ct = default)
    {
        if (TrySpecial(client.Capabilities, client.GetFolder) is { } advertised)
            return advertised;

        var folders = new List<IMailFolder>();
        foreach (var ns in client.PersonalNamespaces)
            folders.AddRange(await client.GetFoldersAsync(ns, false, ct));

        return Scan(folders);
    }

    /// <summary>
    /// The RFC 6154 / XLIST answer, or null when there is none to have.
    /// </summary>
    /// <remarks>
    /// The catch names two types rather than swallowing everything. A bare <c>catch</c> here is
    /// how "this server has no Trash" came to mean "this connection is broken", "this client is
    /// disposed" and "this account is not authenticated" as well — and every one of those makes
    /// the caller expunge instead of moving to a folder that exists.
    /// </remarks>
    private static IMailFolder? TrySpecial(ImapCapabilities capabilities, Func<SpecialFolder, IMailFolder?> special)
    {
        if ((capabilities & (ImapCapabilities.SpecialUse | ImapCapabilities.XList)) == 0)
            return null;

        try
        {
            return special(SpecialFolder.Trash);
        }
        catch (NotSupportedException)
        {
            // Advertised the capability and then refused the lookup. Fall through to the scan.
            return null;
        }
        catch (FolderNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// The trash folder among <paramref name="folders"/>, by attribute first and then by name.
    /// </summary>
    private static IMailFolder? Scan(IEnumerable<IMailFolder> folders)
    {
        var list = folders as IReadOnlyList<IMailFolder> ?? folders.ToList();

        // The attribute is the server's own claim, so it outranks any name we guessed at.
        foreach (var folder in list)
            if (folder.Attributes.HasFlag(FolderAttributes.Trash))
                return folder;

        foreach (var name in TrashNames)
            foreach (var folder in list)
                if (string.Equals(folder.Name, name, StringComparison.OrdinalIgnoreCase))
                    return folder;

        // Gmail without SPECIAL-USE: the leaf is named "Trash", so the loop above already has
        // it — this is the belt-and-braces case of a server that reports only full names.
        foreach (var folder in list)
            if (string.Equals(folder.FullName, GmailTrashPath, StringComparison.OrdinalIgnoreCase))
                return folder;

        return null;
    }
}
