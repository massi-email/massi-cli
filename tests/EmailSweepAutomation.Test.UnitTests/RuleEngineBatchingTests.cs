namespace EmailSweepAutomation.Test.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmailSweepAutomation;
using MailKit;
using MailKit.Net.Imap;
using Moq;
using Xunit;

/// <summary>
/// The act phase sends at most <see cref="RuleEngine.MaxUidsPerCommand"/> UIDs per IMAP command.
/// Gmail answers one MOVE of a few thousand UIDs with <c>NO System Error (Failure)</c> — a real
/// sweep of 12,933 messages died that way — so every mutating command is chunked, and progress
/// is reported per chunk so a failure part-way still says how far it got.
/// </summary>
public class RuleEngineBatchingTests
{
    // 1,201 = two full chunks and one of 201: enough to see the boundary on both sides.
    private const int Many = 1201;

    private static IMessageSummary Message(uint uid)
    {
        var mock = new Mock<IMessageSummary>();
        mock.SetupGet(m => m.UniqueId).Returns(new UniqueId(uid));
        return mock.Object;
    }

    private static List<IMessageSummary> Messages(int count, uint first = 1) =>
        Enumerable.Range(0, count).Select(i => Message(first + (uint)i)).ToList();

    /// <summary>An IProgress that records synchronously, unlike <see cref="Progress{T}"/>.</summary>
    private sealed class Recorder : IProgress<int>
    {
        public List<int> Reports { get; } = [];
        public void Report(int value) => Reports.Add(value);
    }

    private static Mock<IMailFolder> Inbox(List<IList<UniqueId>> calls, int failOnCall = 0)
    {
        var inbox = new Mock<IMailFolder>();
        inbox.SetupGet(f => f.DirectorySeparator).Returns('/');
        inbox.Setup(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IMailFolder>(), It.IsAny<CancellationToken>()))
            .Returns<IList<UniqueId>, IMailFolder, CancellationToken>((uids, _, _) =>
            {
                calls.Add(uids.ToList());
                if (calls.Count == failOnCall)
                    throw new ImapCommandException(ImapCommandResponse.No, "MOVE", "System Error (Failure)");
                return Task.FromResult(UniqueIdMap.Empty);
            });
        return inbox;
    }

    [Fact]
    public void TheChunkSize_Is500()
    {
        // Pinned so a change is a decision rather than a drive-by: the number is what Gmail
        // accepts comfortably, not a performance knob.
        Assert.Equal(500, RuleEngine.MaxUidsPerCommand);
    }

    [Fact]
    public async Task Moves_AreSentInChunksOfAtMost500_AndReportCumulativeProgress()
    {
        var calls = new List<IList<UniqueId>>();
        var inbox = Inbox(calls);
        var dest = new Mock<IMailFolder>().Object;
        inbox.Setup(f => f.GetSubfolderAsync("Orders", It.IsAny<CancellationToken>())).ReturnsAsync(dest);
        var progress = new Recorder();

        var moved = await new RuleEngine().MoveMessagesAsync(
            Messages(Many).Select(m => (m, "Orders")), inbox.Object, progress);

        Assert.Equal(Many, moved);
        Assert.Equal([500, 500, 201], calls.Select(c => c.Count));
        // Every UID exactly once, in order.
        Assert.Equal(Enumerable.Range(1, Many).Select(i => (uint)i), calls.SelectMany(c => c).Select(u => u.Id));
        Assert.Equal([500, 1000, 1201], progress.Reports);
    }

    [Fact]
    public async Task Moves_ToSeveralFolders_ChunkPerFolder_AndProgressRunsAcrossThem()
    {
        var calls = new List<IList<UniqueId>>();
        var inbox = Inbox(calls);
        var a = new Mock<IMailFolder>().Object;
        var b = new Mock<IMailFolder>().Object;
        inbox.Setup(f => f.GetSubfolderAsync("A", It.IsAny<CancellationToken>())).ReturnsAsync(a);
        inbox.Setup(f => f.GetSubfolderAsync("B", It.IsAny<CancellationToken>())).ReturnsAsync(b);
        var progress = new Recorder();

        var moves = Messages(600).Select(m => (m, "A"))
            .Concat(Messages(10, first: 1000).Select(m => (m, "B")));
        var moved = await new RuleEngine().MoveMessagesAsync(moves, inbox.Object, progress);

        Assert.Equal(610, moved);
        Assert.Equal([500, 100, 10], calls.Select(c => c.Count));
        Assert.Equal([500, 600, 610], progress.Reports);
    }

    [Fact]
    public async Task AMoveThatFailsOnTheSecondChunk_HasReportedTheFirst()
    {
        var calls = new List<IList<UniqueId>>();
        var inbox = Inbox(calls, failOnCall: 2);
        var dest = new Mock<IMailFolder>().Object;
        inbox.Setup(f => f.GetSubfolderAsync("Orders", It.IsAny<CancellationToken>())).ReturnsAsync(dest);
        var progress = new Recorder();

        await Assert.ThrowsAsync<ImapCommandException>(() => new RuleEngine().MoveMessagesAsync(
            Messages(Many).Select(m => (m, "Orders")), inbox.Object, progress));

        Assert.Equal(2, calls.Count);           // stopped at the failure, did not carry on
        Assert.Equal([500], progress.Reports);  // only what the server acknowledged
    }

    [Fact]
    public async Task TrashDeletes_AreSentInChunksOfAtMost500()
    {
        var calls = new List<IList<UniqueId>>();
        var inbox = Inbox(calls);
        var trash = new Mock<IMailFolder>();
        var progress = new Recorder();

        var deleted = await new RuleEngine().DeleteMessagesAsync(Messages(Many), inbox.Object, trash.Object, progress);

        Assert.Equal(Many, deleted);
        Assert.Equal([500, 500, 201], calls.Select(c => c.Count));
        Assert.Equal([500, 1000, 1201], progress.Reports);
        inbox.Verify(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), trash.Object, It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ATrashDeleteThatFailsOnTheSecondChunk_HasReportedTheFirst()
    {
        var calls = new List<IList<UniqueId>>();
        var inbox = Inbox(calls, failOnCall: 2);
        var progress = new Recorder();

        await Assert.ThrowsAsync<ImapCommandException>(() => new RuleEngine().DeleteMessagesAsync(
            Messages(Many), inbox.Object, new Mock<IMailFolder>().Object, progress));

        Assert.Equal([500], progress.Reports);
    }

    [Fact]
    public async Task PermanentDeletes_FlagAndExpungeEachChunkBeforeTheNext()
    {
        // Interleaved, not "flag everything, then expunge everything": a failure part-way must
        // leave at most one chunk flagged-but-not-expunged, not the whole remaining selection.
        var log = new List<string>();
        var inbox = new Mock<IMailFolder>();
        inbox.Setup(f => f.StoreAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IStoreFlagsRequest>(), It.IsAny<CancellationToken>()))
            .Returns<IList<UniqueId>, IStoreFlagsRequest, CancellationToken>((uids, _, _) =>
            {
                log.Add($"store {uids.Count}");
                return Task.FromResult<IList<UniqueId>>(new List<UniqueId>());
            });
        inbox.Setup(f => f.ExpungeAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<CancellationToken>()))
            .Returns<IList<UniqueId>, CancellationToken>((uids, _) =>
            {
                log.Add($"expunge {uids.Count}");
                return Task.CompletedTask;
            });
        var progress = new Recorder();

        var deleted = await new RuleEngine().DeleteMessagesAsync(Messages(Many), inbox.Object, null, progress);

        Assert.Equal(Many, deleted);
        Assert.Equal(
            ["store 500", "expunge 500", "store 500", "expunge 500", "store 201", "expunge 201"],
            log);
        Assert.Equal([500, 1000, 1201], progress.Reports);
    }

    [Fact]
    public async Task APermanentDeleteWhoseSecondExpungeFails_CountsOnlyTheFirstChunk()
    {
        // Flagged is not deleted: the chunk whose EXPUNGE failed is still in the mailbox, and a
        // re-run selects it again. Only an expunged chunk counts.
        var expunges = 0;
        var inbox = new Mock<IMailFolder>();
        inbox.Setup(f => f.StoreAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IStoreFlagsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UniqueId>());
        inbox.Setup(f => f.ExpungeAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<CancellationToken>()))
            .Returns(() => ++expunges == 2
                ? throw new ImapCommandException(ImapCommandResponse.No, "EXPUNGE", "System Error")
                : Task.CompletedTask);
        var progress = new Recorder();

        await Assert.ThrowsAsync<ImapCommandException>(
            () => new RuleEngine().DeleteMessagesAsync(Messages(Many), inbox.Object, null, progress));

        Assert.Equal([500], progress.Reports);
        inbox.Verify(f => f.StoreAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IStoreFlagsRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task WithoutAProgressSink_TheActionsStillRun()
    {
        // The CLI's original call shape: no progress argument at all.
        var calls = new List<IList<UniqueId>>();
        var inbox = Inbox(calls);

        var deleted = await new RuleEngine().DeleteMessagesAsync(Messages(Many), inbox.Object, new Mock<IMailFolder>().Object);

        Assert.Equal(Many, deleted);
        Assert.Equal(3, calls.Count);
    }
}
