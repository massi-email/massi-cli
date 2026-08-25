namespace EmailSweepAutomation.Test.UnitTests;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EmailSweepAutomation;
using MailKit;
using Moq;
using Xunit;

public class RuleEngineDeleteTests
{
    private static IMessageSummary Message(uint uid)
    {
        var mock = new Mock<IMessageSummary>();
        mock.SetupGet(m => m.UniqueId).Returns(new UniqueId(uid));
        return mock.Object;
    }

    [Fact]
    public async Task DeleteMessagesAsync_WithTrashFolder_MovesToTrash()
    {
        var engine = new RuleEngine();
        var inbox = new Mock<IMailFolder>();
        var trash = new Mock<IMailFolder>();
        inbox.Setup(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), trash.Object, It.IsAny<CancellationToken>()))
             .ReturnsAsync(UniqueIdMap.Empty);

        await engine.DeleteMessagesAsync(new[] { Message(1) }, inbox.Object, trash.Object);

        inbox.Verify(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), trash.Object, It.IsAny<CancellationToken>()), Times.Once);
        inbox.Verify(f => f.ExpungeAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteMessagesAsync_WithoutTrashFolder_FlagsAndExpunges()
    {
        var engine = new RuleEngine();
        var inbox = new Mock<IMailFolder>();
        inbox.Setup(f => f.StoreAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IStoreFlagsRequest>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new List<UniqueId>());
        inbox.Setup(f => f.ExpungeAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<CancellationToken>()))
             .Returns(Task.CompletedTask);

        await engine.DeleteMessagesAsync(new[] { Message(1) }, inbox.Object, null);

        inbox.Verify(f => f.StoreAsync(
            It.IsAny<IList<UniqueId>>(),
            It.Is<IStoreFlagsRequest>(r =>
                r.Action == StoreAction.Add &&
                (r.Flags & MessageFlags.Deleted) != 0 &&
                r.Silent),
            It.IsAny<CancellationToken>()), Times.Once);
        inbox.Verify(f => f.ExpungeAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<CancellationToken>()), Times.Once);
        inbox.Verify(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IMailFolder>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteMessagesAsync_NoMessages_DoesNothing()
    {
        var engine = new RuleEngine();
        var inbox = new Mock<IMailFolder>();

        await engine.DeleteMessagesAsync(new IMessageSummary[0], inbox.Object, null);

        inbox.Verify(f => f.MoveToAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IMailFolder>(), It.IsAny<CancellationToken>()), Times.Never);
        inbox.Verify(f => f.StoreAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<IStoreFlagsRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        inbox.Verify(f => f.ExpungeAsync(It.IsAny<IList<UniqueId>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
