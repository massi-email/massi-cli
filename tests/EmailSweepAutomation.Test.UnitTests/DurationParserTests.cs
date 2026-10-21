namespace EmailSweepAutomation.Test.UnitTests;

using System;
using EmailSweepAutomation;
using Xunit;

public class DurationParserTests
{
    [Theory]
    [InlineData("30d", 30)]
    [InlineData("7D", 7)]   // case-insensitive
    [InlineData("1d", 1)]
    public void Parse_DayShorthand_ReturnsExpectedTimeSpan(string input, int days)
    {
        Assert.Equal(TimeSpan.FromDays(days), DurationParser.Parse(input));
    }

    [Theory]
    [InlineData("90.00:00:00.0000000", 90)]
    [InlineData("1.00:00:00", 1)]
    public void Parse_DotNetTimeSpanAtLeastOneDay_ReturnsExpectedTimeSpan(string input, int days)
    {
        Assert.Equal(TimeSpan.FromDays(days), DurationParser.Parse(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0d")]
    [InlineData("-5d")]
    [InlineData("12h")]            // hours no longer accepted
    [InlineData("30m")]            // minutes no longer accepted
    [InlineData("0.12:00:00")]     // 12 hours via TimeSpan -> under 1 day
    [InlineData("2147483648d")]    // overflows int
    [InlineData("999999999d")]     // overflows TimeSpan
    public void Parse_InvalidOrUnderOneDay_Throws(string? input)
    {
        Assert.Throws<FormatException>(() => DurationParser.Parse(input!));
    }
}
