namespace EmailSweepAutomation.Test.UnitTests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EmailSweepAutomation;
using MailKit;
using Moq;
using Xunit;

/// <summary>
/// The act half of a move: resolving a rule's destination path against a server whose
/// hierarchy delimiter we only learn at connect time, and reporting what actually moved.
/// </summary>
public class RuleEngineMoveTests
{
    private static IMessageSummary Message(uint uid)
    {
        var mock = new Mock<IMessageSummary>();
        mock.SetupGet(m => m.UniqueId).Returns(new UniqueId(uid));
        return mock.Object;
    }

    /// <summary>A folder that already exists, with the given hierarchy delimiter.</summary>
    private static Mock<IMailFolder> Folder(char separator = '/')
    {
        var mock = new Mock<IMailFolder>();
        mock.SetupGet(f => f.DirectorySeparator).Returns(separator);
        mock.SetupGet(f => f.FullName).Returns("INBOX");
        mock.Setup(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IMailFolder>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UniqueIdMap.Empty);
        return mock;
    }

    private static (IMessageSummary Message, string Folder) Match(uint uid, string folder) => (Message(uid), folder);

    [Fact]
    public async Task MoveMessagesAsync_ExistingFolder_MovesThere()
    {
        var engine = new RuleEngine();
        var inbox = Folder();
        var archive = Folder().Object;
        inbox.Setup(f => f.GetSubfolderAsync("Archive", It.IsAny<CancellationToken>())).ReturnsAsync(archive);

        var moved = await engine.MoveMessagesAsync([Match(1, "Archive")], inbox.Object);

        Assert.Equal(1, moved);
        inbox.Verify(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), archive, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MoveMessagesAsync_MissingFolder_CreatesItThenMoves()
    {
        var engine = new RuleEngine();
        var inbox = Folder();
        var created = Folder().Object;
        inbox.Setup(f => f.GetSubfolderAsync("Archive", It.IsAny<CancellationToken>()))
             .ThrowsAsync(new FolderNotFoundException("Archive"));
        inbox.Setup(f => f.CreateAsync("Archive", true, It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var moved = await engine.MoveMessagesAsync([Match(1, "Archive")], inbox.Object);

        Assert.Equal(1, moved);
        inbox.Verify(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), created, It.IsAny<CancellationToken>()), Times.Once);
    }

    // "Archive/2026" used to reach GetSubfolder whole, which throws ArgumentException for a name
    // containing the delimiter — caught by a blanket handler that returned null, so the move was
    // skipped in silence and the run still reported it as done.
    [Fact]
    public async Task MoveMessagesAsync_NestedPath_WalksOneLevelAtATime()
    {
        var engine = new RuleEngine();
        var inbox = Folder();
        var archive = Folder();
        var year = Folder().Object;
        inbox.Setup(f => f.GetSubfolderAsync("Archive", It.IsAny<CancellationToken>())).ReturnsAsync(archive.Object);
        archive.Setup(f => f.GetSubfolderAsync("2026", It.IsAny<CancellationToken>())).ReturnsAsync(year);

        var moved = await engine.MoveMessagesAsync([Match(1, "Archive/2026")], inbox.Object);

        Assert.Equal(1, moved);
        inbox.Verify(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), year, It.IsAny<CancellationToken>()), Times.Once);
    }

    // Dovecot and Courier commonly delimit with '.', so "Receipts.Amazon" is not a folder name
    // that server can hold. Saying so beats moving nothing and calling the run a success.
    [Fact]
    public async Task MoveMessagesAsync_SegmentContainsServerDelimiter_ThrowsNamingIt()
    {
        var engine = new RuleEngine();
        var inbox = Folder(separator: '.');

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.MoveMessagesAsync([Match(1, "Receipts.Amazon")], inbox.Object));

        Assert.Contains("Receipts.Amazon", ex.Message);
        Assert.Contains('.', ex.Message);
        inbox.Verify(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IMailFolder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MoveMessagesAsync_UnresolvableFolder_ThrowsBeforeMovingAnything()
    {
        var engine = new RuleEngine();
        var inbox = Folder();
        var good = Folder().Object;
        inbox.Setup(f => f.GetSubfolderAsync("Good", It.IsAny<CancellationToken>())).ReturnsAsync(good);
        inbox.Setup(f => f.GetSubfolderAsync("Bad", It.IsAny<CancellationToken>()))
             .ThrowsAsync(new FolderNotFoundException("Bad"));
        inbox.Setup(f => f.CreateAsync("Bad", true, It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("server said no"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.MoveMessagesAsync([Match(1, "Good"), Match(2, "Bad")], inbox.Object));

        // Every destination is resolved before the first move, so one bad folder cannot leave
        // half the rules applied.
        inbox.Verify(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IMailFolder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MoveMessagesAsync_GroupsMovesByDestination()
    {
        var engine = new RuleEngine();
        var inbox = Folder();
        var a = Folder().Object;
        var b = Folder().Object;
        inbox.Setup(f => f.GetSubfolderAsync("A", It.IsAny<CancellationToken>())).ReturnsAsync(a);
        inbox.Setup(f => f.GetSubfolderAsync("B", It.IsAny<CancellationToken>())).ReturnsAsync(b);
        var m1 = Message(1);
        var m2 = Message(2);
        var m3 = Message(3);

        var moved = await engine.MoveMessagesAsync(
            new[] { (m1, "A"), (m2, "A"), (m3, "B") }, inbox.Object);

        Assert.Equal(3, moved);
        inbox.Verify(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), a, It.IsAny<CancellationToken>()), Times.Once);
        inbox.Verify(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), b, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MoveMessagesAsync_NoMatches_MovesNothing()
    {
        var engine = new RuleEngine();
        var inbox = Folder();

        var moved = await engine.MoveMessagesAsync([], inbox.Object);

        Assert.Equal(0, moved);
        inbox.Verify(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IMailFolder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // MailKit declares CreateAsync as Task<IMailFolder?>, so "created it" and "got nothing back"
    // are different outcomes. Returning the null onward makes the next MoveToAsync throw a bare
    // NullReferenceException naming nothing the user could act on.
    [Fact]
    public async Task MoveMessagesAsync_FolderCreateReturnsNothing_ThrowsNamingTheFolder()
    {
        var engine = new RuleEngine();
        var inbox = Folder();
        inbox.Setup(f => f.GetSubfolderAsync("Archive", It.IsAny<CancellationToken>()))
             .ThrowsAsync(new FolderNotFoundException("Archive"));
        inbox.Setup(f => f.CreateAsync("Archive", true, It.IsAny<CancellationToken>()))
             .ReturnsAsync((IMailFolder?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.MoveMessagesAsync([Match(1, "Archive")], inbox.Object));

        Assert.Contains("Archive", ex.Message);
        inbox.Verify(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IMailFolder>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
