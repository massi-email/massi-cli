namespace EmailSweepAutomation.Test.UnitTests;

using Xunit;

public class SenderMatchTests
{
    [Theory]
    [InlineData("a@shop.com", "shop.com")]
    [InlineData("a@b@shop.com", "shop.com")] // the last @ splits
    [InlineData("a@", null)]
    [InlineData("shop.com", null)]
    public void Host_IsWhatFollowsTheLastAt(string address, string? expected)
        => Assert.Equal(expected, SenderMatch.Host(address));

    [Theory]
    [InlineData("a@shop.com", "shop.com", true)]
    [InlineData("a@notice.shop.com", "shop.com", true)]
    [InlineData("a@SHOP.COM", "shop.com", true)]
    [InlineData("a@notshop.com", "shop.com", false)]
    [InlineData("a@shop.com.evil.io", "shop.com", false)]
    [InlineData("shop.com", "shop.com", false)] // no @: not an address
    public void DomainMatches_IsTheDomainOrASubdomain(string address, string domain, bool expected)
        => Assert.Equal(expected, SenderMatch.DomainMatches(address, domain));

    [Theory]
    [InlineData("deals@mail.shop.com", "deals@mail.shop.com", true)]
    [InlineData("DEALS@mail.shop.com", "deals@MAIL.shop.com", true)]
    [InlineData("other@mail.shop.com", "deals@mail.shop.com", false)]
    [InlineData("x@mail.shop.com", "@mail.shop.com", true)]
    [InlineData("x@a.mail.shop.com", "@mail.shop.com", false)] // @host is exact
    [InlineData("x@shop.com", "@*.shop.com", true)]
    [InlineData("x@a.mail.shop.com", "@*.shop.com", true)]
    [InlineData("x@notshop.com", "@*.shop.com", false)]
    public void EntryMatches_ByEntryForm(string address, string entry, bool expected)
        => Assert.Equal(expected, SenderMatch.EntryMatches(address, entry));

    [Theory]
    [InlineData("shop.com", true)]
    [InlineData("mail.shop.co.uk", true)]
    [InlineData("shop", false)]
    [InlineData(".shop.com", false)]
    [InlineData("shop.com.", false)]
    [InlineData("sh op.com", false)]
    [InlineData("a@shop.com", false)]
    [InlineData("*.shop.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidHost(string? host, bool expected) => Assert.Equal(expected, SenderMatch.IsValidHost(host));

    [Theory]
    [InlineData("deals@mail.shop.com", true)]
    [InlineData("@mail.shop.com", true)]
    [InlineData("@*.shop.com", true)]
    [InlineData("@*shop.com", false)]
    [InlineData("@shop", false)]
    [InlineData("deals@", false)]
    [InlineData("@", false)]
    [InlineData("de als@shop.com", false)]
    [InlineData("a@b@shop.com", false)]
    [InlineData(" ", false)]
    [InlineData("promo*@shop.com", true)]
    [InlineData("*promo@shop.com", true)]
    [InlineData("*promo*@shop.com", true)]
    [InlineData("*@shop.com", false)]          // "@shop.com" says that
    [InlineData("**@shop.com", false)]
    [InlineData("pro*mo@shop.com", false)]     // a star mid-name used to pass and never match
    [InlineData("*pro*mo*@shop.com", false)]
    [InlineData("**promo@shop.com", false)]
    [InlineData("promo*@*.shop.com", false)]
    [InlineData("promo*@shop", false)]
    [InlineData("pro mo*@shop.com", false)]
    public void IsValidEntry(string entry, bool expected) => Assert.Equal(expected, SenderMatch.IsValidEntry(entry));

    [Theory]
    // prefix*@host: starts with
    [InlineData("ae-market.ae4@deals.shop.com", "ae-market*@deals.shop.com", true)]
    [InlineData("ae-market@deals.shop.com", "ae-market*@deals.shop.com", true)]      // the star may match nothing
    [InlineData("AE-Market5@Deals.Shop.com", "ae-market*@deals.shop.com", true)]
    [InlineData("aeug-market4@deals.shop.com", "ae-market*@deals.shop.com", false)]
    [InlineData("ae-market4@mail.shop.com", "ae-market*@deals.shop.com", false)]     // another host
    [InlineData("ae-market4@x.deals.shop.com", "ae-market*@deals.shop.com", false)]  // the host is exact
    // *suffix@host: ends with
    [InlineData("weekly-deals@shop.com", "*-deals@shop.com", true)]
    [InlineData("weekly-deals2@shop.com", "*-deals@shop.com", false)]
    // *core*@host: contains
    [InlineData("aeug-best-care-market16@deals.shop.com", "*best-care-market*@deals.shop.com", true)]
    [InlineData("best-care-market@deals.shop.com", "*best-care-market*@deals.shop.com", true)]
    [InlineData("ae-best-care-mark16@deals.shop.com", "*best-care-market*@deals.shop.com", false)]
    // only the local part is searched, never the host
    [InlineData("x@deals.shop.com", "*deals*@deals.shop.com", false)]
    [InlineData("no-at-sign", "no*@shop.com", false)]
    public void EntryMatches_LocalPartPatterns(string address, string entry, bool expected)
        => Assert.Equal(expected, SenderMatch.EntryMatches(address, entry));

    [Theory]
    [InlineData("promo*@shop.com", true)]
    [InlineData("*promo@shop.com", true)]
    [InlineData("*promo*@shop.com", true)]
    [InlineData("promo@shop.com", false)]
    [InlineData("@shop.com", false)]
    [InlineData("@*.shop.com", false)]
    [InlineData("pro*mo@shop.com", false)]
    [InlineData("*@shop.com", false)]
    // malformed: a host with no dot, a space in the name
    [InlineData("promo*@shop", false)]
    [InlineData("pro mo*@shop.com", false)]
    public void IsPattern(string entry, bool expected) => Assert.Equal(expected, SenderMatch.IsPattern(entry));
}
