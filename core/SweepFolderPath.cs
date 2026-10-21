namespace EmailSweepAutomation;

/// <summary>
/// How a rule's <see cref="SweepRule.MoveToFolder"/> is read as a folder path.
/// </summary>
/// <remarks>
/// A destination is written with <c>/</c> between levels — <c>Archive/2026</c> — and that is
/// the product's separator, not the server's. IMAP servers each pick their own hierarchy
/// delimiter (Gmail uses <c>/</c>, Dovecot and Courier commonly use <c>.</c>) and MailKit only
/// exposes it once a folder is open, so no rule written offline can know it. The engine walks
/// these segments one level at a time and lets each server spell the result its own way.
/// </remarks>
public static class SweepFolderPath
{
    /// <summary>
    /// The levels of <paramref name="path"/>, trimmed, with blank levels dropped. Empty when the
    /// path names no folder at all — <c>""</c>, <c>"  "</c>, <c>"/"</c>.
    /// </summary>
    public static IReadOnlyList<string> Segments(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return [];

        return path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Trim())
            .Where(segment => segment.Length > 0)
            .ToList();
    }
}
