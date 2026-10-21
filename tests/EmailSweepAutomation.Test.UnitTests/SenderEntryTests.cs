namespace EmailSweepAutomation.Test.UnitTests;

using Xunit;

public class SenderEntryTests
{
    [Fact]
    public void Parse_HostAndSubdomains() =>
        Assert.Equal(new HostAndSubdomains("shop.com"), SenderEntry.Parse("@*.shop.com"));

    [Fact]
    public void Parse_ExactHost() =>
        Assert.Equal(new ExactHost("mail.shop.com"), SenderEntry.Parse("@mail.shop.com"));

    [Fact]
    public void Parse_ExactAddress() =>
        Assert.Equal(new ExactAddress("deals@mail.shop.com"), SenderEntry.Parse("deals@mail.shop.com"));

    [Theory]
    [InlineData("promo*@shop.com", "promo", false, true)]
    [InlineData("*promo@shop.com", "promo", true, false)]
    [InlineData("*promo*@shop.com", "promo", true, true)]
    public void Parse_NamePattern(string entry, string literal, bool openStart, bool openEnd) =>
        Assert.Equal(new NamePattern(literal, openStart, openEnd, "shop.com"), SenderEntry.Parse(entry));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("@")]
    [InlineData("@shop")]            // a host needs a dot
    [InlineData("@*shop.com")]
    [InlineData("@*.shop")]
    [InlineData("deals@")]
    [InlineData("deals")]
    [InlineData(" deals@shop.com")]  // trimming is the caller's
    [InlineData("de als@shop.com")]
    [InlineData("a@b@shop.com")]
    [InlineData("*@shop.com")]       // "@shop.com" says that
    [InlineData("**@shop.com")]
    [InlineData("pro*mo@shop.com")]
    [InlineData("*pro*mo*@shop.com")]
    [InlineData("**promo@shop.com")]
    [InlineData("promo*@*.shop.com")]
    [InlineData("promo*@shop")]
    [InlineData("pro mo*@shop.com")]
    [InlineData("a@b*@shop.com")]
    public void Parse_IsNullForNoValidEntry(string? entry) => Assert.Null(SenderEntry.Parse(entry));

    [Theory]
    [InlineData("@*.shop.com", "x@a.mail.shop.com", true)]
    [InlineData("@*.shop.com", "x@notshop.com", false)]
    [InlineData("@mail.shop.com", "x@MAIL.shop.com", true)]
    [InlineData("@mail.shop.com", "x@a.mail.shop.com", false)]
    [InlineData("deals@shop.com", "DEALS@shop.com", true)]
    [InlineData("promo*@shop.com", "promo12@shop.com", true)]
    [InlineData("promo*@shop.com", "promo12@x.shop.com", false)]
    public void Matches_ByKind(string entry, string address, bool expected) =>
        Assert.Equal(expected, SenderEntry.Parse(entry)!.Matches(address));

    [Theory]
    // No valid entry still matches as the engine always read it, for rules stored before validation.
    [InlineData("x@shop", "@shop", true)]
    [InlineData("x@a.shop", "@*.shop", true)]
    [InlineData("promo1@shop", "promo*@shop", true)]
    [InlineData("pro*mo@shop.com", "pro*mo@shop.com", true)]
    [InlineData("pro1mo@shop.com", "pro*mo@shop.com", false)]
    public void EntryMatches_ReadsNoValidEntryByItsShape(string address, string entry, bool expected) =>
        Assert.Equal(expected, SenderMatch.EntryMatches(address, entry));
}
