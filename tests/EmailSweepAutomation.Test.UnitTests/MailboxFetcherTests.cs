namespace EmailSweepAutomation.Test.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmailSweepAutomation;
using MailKit;
using MailKit.Search;
using Moq;
using Xunit;

public class MailboxFetcherTests
{
    private const MessageSummaryItems Items = MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId;

    private static IMessageSummary Msg(uint uid)
    {
        var m = new Mock<IMessageSummary>();
        m.SetupGet(x => x.UniqueId).Returns(new UniqueId(uid));
        return m.Object;
    }

    private static Mock<IMailFolder> FolderWithCount(int count)
    {
        var folder = new Mock<IMailFolder>();
        folder.SetupGet(f => f.Count).Returns(count);
        // Index fetch: return one distinct message per index in [min, max]; uid = index + 1 (uids are 1-based).
        folder.Setup(f => f.FetchAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IFetchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int min, int max, IFetchRequest _, CancellationToken _) =>
                (IList<IMessageSummary>)Enumerable.Range(min, max - min + 1)
                    .Select(i => Msg((uint)(i + 1))).ToList());
        return folder;
    }

    [Fact]
    public async Task FetchAsync_Count_NoDuplicatesAtBatchBoundaries()
    {
        var folder = FolderWithCount(120); // 120 > batch size of 100 -> two batches
        var fetcher = new MailboxFetcher();

        var result = await fetcher.FetchAsync(
            folder.Object, new FetchSelection.ByCount(-1), Items, progress: null, CancellationToken.None);

        Assert.Equal(120, result.Count);
        Assert.Equal(120, result.Select(m => m.UniqueId).Distinct().Count());
    }

    [Fact]
    public async Task FetchAsync_Count_LargerThanInbox_DoesNotThrowAndReturnsAll()
    {
        var folder = FolderWithCount(120);
        var fetcher = new MailboxFetcher();

        // Regression: count > inbox size used to compute a negative min index and throw.
        var result = await fetcher.FetchAsync(
            folder.Object, new FetchSelection.ByCount(2000), Items, progress: null, CancellationToken.None);

        Assert.Equal(120, result.Count);
    }

    [Fact]
    public async Task FetchAsync_SentSince_SearchesAndFetchesReturnedUids()
    {
        var uids = Enumerable.Range(1, 150).Select(i => new UniqueId((uint)i)).ToList();
        var folder = new Mock<IMailFolder>();
        SearchQuery? capturedQuery = null;
        folder.Setup(f => f.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .Callback<SearchQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync((IList<UniqueId>)uids);
        folder.Setup(f => f.FetchAsync(
                It.IsAny<IList<UniqueId>>(), It.IsAny<IFetchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IList<UniqueId> u, IFetchRequest _, CancellationToken _) =>
                (IList<IMessageSummary>)u.Select(x => Msg(x.Id)).ToList());

        var fetcher = new MailboxFetcher();
        var cutoff = new DateTimeOffset(2026, 5, 4, 0, 0, 0, TimeSpan.Zero);

        var result = await fetcher.FetchAsync(
            folder.Object, new FetchSelection.BySentSince(cutoff), Items, progress: null, CancellationToken.None);

        Assert.Equal(150, result.Count);
        folder.Verify(f => f.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        var dateQuery = Assert.IsType<DateSearchQuery>(capturedQuery);
        Assert.Equal(SearchTerm.SentSince, dateQuery.Term);
        Assert.Equal(cutoff.Date, dateQuery.Date);
        folder.Verify(f => f.FetchAsync(
            It.IsAny<IList<UniqueId>>(), It.IsAny<IFetchRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task FetchAsync_ByUids_FetchesExactlyWhatItWasGiven_InBatches()
    {
        // The caller has already decided which messages it wants — here, a sample spread across
        // one sender's whole history — and the fetcher must neither widen that set nor search
        // for one of its own.
        var wanted = new[] { 3u, 40u, 900u, 12_000u }.Select(id => new UniqueId(id)).ToList();
        var folder = new Mock<IMailFolder>();
        var requested = new List<UniqueId>();
        folder.Setup(f => f.FetchAsync(
                It.IsAny<IList<UniqueId>>(), It.IsAny<IFetchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IList<UniqueId>, IFetchRequest, CancellationToken>((u, _, _) => requested.AddRange(u))
            .ReturnsAsync((IList<UniqueId> u, IFetchRequest _, CancellationToken _) =>
                (IList<IMessageSummary>)u.Select(x => Msg(x.Id)).ToList());

        var fetcher = new MailboxFetcher();

        var result = await fetcher.FetchAsync(
            folder.Object, new FetchSelection.ByUids(wanted), Items, progress: null, CancellationToken.None);

        Assert.Equal(wanted, requested);
        Assert.Equal(wanted, result.Select(m => m.UniqueId));
        folder.Verify(f => f.SearchAsync(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FetchAsync_ByUids_BatchesRatherThanAskingForEverythingAtOnce()
    {
        var wanted = Enumerable.Range(1, 250).Select(i => new UniqueId((uint)i)).ToList();
        var folder = new Mock<IMailFolder>();
        folder.Setup(f => f.FetchAsync(
                It.IsAny<IList<UniqueId>>(), It.IsAny<IFetchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IList<UniqueId> u, IFetchRequest _, CancellationToken _) =>
                (IList<IMessageSummary>)u.Select(x => Msg(x.Id)).ToList());

        var fetcher = new MailboxFetcher();

        var result = await fetcher.FetchAsync(
            folder.Object, new FetchSelection.ByUids(wanted), Items, progress: null, CancellationToken.None);

        Assert.Equal(250, result.Count);
        folder.Verify(f => f.FetchAsync(
            It.IsAny<IList<UniqueId>>(), It.IsAny<IFetchRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task FetchAsync_ReportsMonotonicProgressEndingAtTotal()
    {
        var folder = FolderWithCount(150);
        var fetcher = new MailboxFetcher();

        var sync = new SyncProgress();
        var result = await fetcher.FetchAsync(
            folder.Object, new FetchSelection.ByCount(-1), Items, sync, CancellationToken.None);

        Assert.Equal(150, result.Count);
        Assert.NotEmpty(sync.Reports);
        Assert.Equal(new FetchProgress(150, 150), sync.Reports[^1]);
        for (int i = 1; i < sync.Reports.Count; i++)
            Assert.True(sync.Reports[i].Fetched >= sync.Reports[i - 1].Fetched);
    }

    private sealed class SyncProgress : IProgress<FetchProgress>
    {
        public List<FetchProgress> Reports { get; } = new();
        public void Report(FetchProgress value) => Reports.Add(value);
    }
}
