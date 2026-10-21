namespace EmailSweepAutomation.Test.UnitTests;

using System;
using System.Collections.Generic;
using EmailSweepAutomation;
using MailKit;
using MailKit.Net.Imap;
using Moq;
using Xunit;

/// <summary>
/// Which folder a sweep's deletes go to — and therefore whether they can be undone at all.
/// A null answer makes <see cref="IRuleEngine.DeleteMessagesAsync"/> flag and expunge, so
/// every case here that wrongly returns null is a case that destroys mail permanently.
/// </summary>
public class TrashFolderResolverTests
{
    private static IMailFolder Folder(string name, string? fullName = null, FolderAttributes attributes = FolderAttributes.None)
    {
        var mock = new Mock<IMailFolder>();
        mock.SetupGet(f => f.Name).Returns(name);
        mock.SetupGet(f => f.FullName).Returns(fullName ?? name);
        mock.SetupGet(f => f.Attributes).Returns(attributes);
        return mock.Object;
    }

    /// <summary>A special-folder lookup that must never be reached.</summary>
    private static Func<SpecialFolder, IMailFolder?> NeverAsked =>
        _ => throw new InvalidOperationException("The special-folder lookup should not have been called.");

    [Fact]
    public void SpecialUseAdvertised_AndFound_WinsWithoutScanning()
    {
        var trash = Folder("Trash");

        var resolved = TrashFolderResolver.Resolve(
            ImapCapabilities.SpecialUse, _ => trash, [Folder("Deleted Items")]);

        Assert.Same(trash, resolved);
    }

    [Fact]
    public void SpecialUseAdvertised_ButNoTrashFlagged_FallsBackToTheScan()
    {
        // MailKit returns null, not an exception, when the server advertises SPECIAL-USE and
        // flags no trash mailbox. Treating that as "no Trash" expunged mail on a mailbox that
        // has one.
        var trash = Folder("Trash");

        var resolved = TrashFolderResolver.Resolve(
            ImapCapabilities.SpecialUse, _ => null, [Folder("Archive"), trash]);

        Assert.Same(trash, resolved);
    }

    [Fact]
    public void NoSpecialUseExtension_FindsAFolderNamedDeletedItems()
    {
        // The case that made this class exist: with neither extension advertised, MailKit
        // throws NotSupportedException, and a bare catch called every such server trash-less.
        var deleted = Folder("Deleted Items");

        var resolved = TrashFolderResolver.Resolve(
            ImapCapabilities.None, NeverAsked, [Folder("Archive"), deleted]);

        Assert.Same(deleted, resolved);
    }

    [Fact]
    public void ASpecialUseLookupThatThrowsNotSupported_StillScans()
    {
        var trash = Folder("Bin");

        var resolved = TrashFolderResolver.Resolve(
            ImapCapabilities.SpecialUse,
            _ => throw new NotSupportedException("The IMAP server does not support the SPECIALUSE extension."),
            [trash]);

        Assert.Same(trash, resolved);
    }

    [Fact]
    public void ASpecialUseLookupThatThrowsFolderNotFound_StillScans()
    {
        var trash = Folder("Trash");

        var resolved = TrashFolderResolver.Resolve(
            ImapCapabilities.XList, _ => throw new FolderNotFoundException("Trash"), [trash]);

        Assert.Same(trash, resolved);
    }

    [Fact]
    public void TheTrashAttribute_OutranksAName()
    {
        // The server's own claim beats a name we guessed at.
        var flagged = Folder("Papelera", attributes: FolderAttributes.Trash);

        var resolved = TrashFolderResolver.Resolve(
            ImapCapabilities.None, NeverAsked, [Folder("Trash-2024"), flagged, Folder("Deleted")]);

        Assert.Same(flagged, resolved);
    }

    [Theory]
    [InlineData("Trash")]
    [InlineData("Deleted Items")]
    [InlineData("Deleted Messages")]
    [InlineData("Deleted")]
    [InlineData("Bin")]
    [InlineData("TRASH")]
    [InlineData("deleted items")]
    public void EveryKnownName_IsRecognised(string name)
    {
        var trash = Folder(name);

        Assert.Same(trash, TrashFolderResolver.Resolve(
            ImapCapabilities.None, NeverAsked, [Folder("Archive"), trash]));
    }

    [Fact]
    public void GmailsNestedTrash_IsFoundByItsFullName()
    {
        var trash = Folder("Trash", fullName: "[Gmail]/Trash");

        Assert.Same(trash, TrashFolderResolver.Resolve(
            ImapCapabilities.None, NeverAsked, [Folder("INBOX"), trash]));
    }

    [Fact]
    public void EarlierNamesWin_SoTrashBeatsBin()
    {
        var trash = Folder("Trash");

        Assert.Same(trash, TrashFolderResolver.Resolve(
            ImapCapabilities.None, NeverAsked, [Folder("Bin"), trash]));
    }

    [Fact]
    public void NothingResemblingTrash_IsNull()
    {
        // The honest answer, and the one that makes the run row say DeletesArePermanent.
        Assert.Null(TrashFolderResolver.Resolve(
            ImapCapabilities.None, NeverAsked, [Folder("INBOX"), Folder("Archive")]));
    }

    [Fact]
    public void NoFoldersAtAll_IsNull()
        => Assert.Null(TrashFolderResolver.Resolve(ImapCapabilities.None, NeverAsked, []));

    [Fact]
    public void AProtocolFailureDuringTheScan_Propagates()
    {
        // A dropped connection is not "this mailbox has no Trash". Swallowing it here is
        // exactly how the old bare catch turned a broken session into a permanent delete.
        var folders = ThrowingFolders(new ImapProtocolException("connection dropped"));

        Assert.Throws<ImapProtocolException>(
            () => TrashFolderResolver.Resolve(ImapCapabilities.None, NeverAsked, folders));
    }

    [Fact]
    public void AProtocolFailureFromTheSpecialUseLookup_Propagates()
    {
        Assert.Throws<ServiceNotConnectedException>(() => TrashFolderResolver.Resolve(
            ImapCapabilities.SpecialUse,
            _ => throw new ServiceNotConnectedException("not connected"),
            [Folder("Trash")]));
    }

    private static IEnumerable<IMailFolder> ThrowingFolders(Exception ex)
    {
        yield return Folder("INBOX");
        throw ex;
    }
}
