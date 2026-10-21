using System;
using System.Collections.Generic;
using MailKit;

namespace EmailSweepAutomation;

/// <summary>How a command selects which messages to fetch. The variant encodes the mode.</summary>
public abstract record FetchSelection
{
    /// <summary>Fetch the newest <paramref name="Count"/> messages by index; -1 means all.</summary>
    public sealed record ByCount(int Count) : FetchSelection;

    /// <summary>Fetch messages whose sent date is on or after <paramref name="Cutoff"/>.</summary>
    public sealed record BySentSince(DateTimeOffset Cutoff) : FetchSelection;

    /// <summary>
    /// Fetch exactly these messages. The caller has already decided which — a search it ran
    /// itself, a subset it sampled — and the fetcher only batches them.
    /// </summary>
    public sealed record ByUids(IReadOnlyList<UniqueId> Uids) : FetchSelection;
}

/// <summary>Progress of a fetch: messages retrieved so far out of the total to retrieve.</summary>
public sealed record FetchProgress(int Fetched, int Total);

/// <summary>
/// Validates and resolves the <c>--count</c> / <c>--since</c> options into a
/// <see cref="FetchSelection"/>. Duration is the default mode (last 30 days).
/// </summary>
public static class FetchSelectionResolver
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(30);

    /// <summary>Returns an error message if the option combination is invalid, otherwise null.</summary>
    public static string? Validate(int? count, string? since)
    {
        if (count.HasValue && since is not null)
            return "Specify only one of --count or --since.";

        if (since is not null)
        {
            try { DurationParser.Parse(since); }
            catch (FormatException ex) { return ex.Message; }
        }

        if (count.HasValue && count.Value is not (-1 or > 0))
            return "Count must be greater than 0 or -1 for all emails.";

        return null;
    }

    /// <summary>Resolves the selection. Assumes <see cref="Validate"/> already passed.</summary>
    public static FetchSelection Resolve(int? count, string? since, DateTimeOffset now)
    {
        if (since is not null)
            return new FetchSelection.BySentSince(now - DurationParser.Parse(since));

        if (count.HasValue)
            return new FetchSelection.ByCount(count.Value);

        return new FetchSelection.BySentSince(now - DefaultWindow);
    }
}
