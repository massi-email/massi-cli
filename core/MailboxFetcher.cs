using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Search;

namespace EmailSweepAutomation;

/// <summary>
/// Fetches message summaries from an opened mailbox folder according to a
/// <see cref="FetchSelection"/>. The count path fetches the newest N messages in
/// non-overlapping index batches; every other path resolves to a set of UIDs — from a
/// server-side search, or handed straight in by the caller — and fetches those in batches.
/// </summary>
public sealed class MailboxFetcher
{
    private const int BatchSize = 100;

    public async Task<IList<IMessageSummary>> FetchAsync(
        IMailFolder inbox,
        FetchSelection selection,
        MessageSummaryItems items,
        IProgress<FetchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var request = new FetchRequest { Items = items };

        return selection switch
        {
            // SENTSINCE matches on the Date: header with per-calendar-day granularity.
            FetchSelection.BySentSince since =>
                await FetchBySearchAsync(
                    inbox, SearchQuery.SentSince(since.Cutoff.Date), request, progress, cancellationToken),
            FetchSelection.ByUids byUids =>
                await FetchByUidsAsync(inbox, byUids.Uids, request, progress, cancellationToken),
            FetchSelection.ByCount byCount =>
                await FetchByCountAsync(inbox, byCount.Count, request, progress, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(selection)),
        };
    }

    private static async Task<IList<IMessageSummary>> FetchBySearchAsync(
        IMailFolder inbox,
        SearchQuery query,
        FetchRequest request,
        IProgress<FetchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var uids = await inbox.SearchAsync(query, cancellationToken);
        return await FetchByUidsAsync(inbox, [.. uids], request, progress, cancellationToken);
    }

    private static async Task<IList<IMessageSummary>> FetchByUidsAsync(
        IMailFolder inbox,
        IReadOnlyList<UniqueId> uids,
        FetchRequest request,
        IProgress<FetchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var result = new List<IMessageSummary>(uids.Count);
        for (int i = 0; i < uids.Count; i += BatchSize)
        {
            int length = Math.Min(BatchSize, uids.Count - i);
            var slice = new List<UniqueId>(length);
            for (int j = 0; j < length; j++)
                slice.Add(uids[i + j]);

            var batch = await inbox.FetchAsync(slice, request, cancellationToken);
            result.AddRange(batch);
            progress?.Report(new FetchProgress(result.Count, uids.Count));
        }

        return result;
    }

    private static async Task<IList<IMessageSummary>> FetchByCountAsync(
        IMailFolder inbox,
        int count,
        FetchRequest request,
        IProgress<FetchProgress>? progress,
        CancellationToken cancellationToken)
    {
        int total = inbox.Count;
        int toFetch = count == -1 ? total : Math.Min(count, total); // clamp: never exceed inbox
        int lower = total - toFetch;                                // >= 0 because toFetch <= total

        var result = new List<IMessageSummary>(toFetch);
        for (int start = lower; start < total; start += BatchSize)
        {
            int end = Math.Min(start + BatchSize - 1, total - 1);   // inclusive, non-overlapping
            var batch = await inbox.FetchAsync(start, end, request, cancellationToken);
            result.AddRange(batch);
            progress?.Report(new FetchProgress(result.Count, toFetch));
        }

        return result;
    }
}
