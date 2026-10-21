namespace EmailSweepAutomation.Test.UnitTests;

using System;
using EmailSweepAutomation;
using Xunit;

public class FetchSelectionResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validate_BothCountAndSince_ReturnsError()
    {
        var error = FetchSelectionResolver.Validate(count: 100, since: "30d");
        Assert.Equal("Specify only one of --count or --since.", error);
    }

    [Fact]
    public void Validate_OnlySince_ReturnsNull()
    {
        Assert.Null(FetchSelectionResolver.Validate(count: null, since: "30d"));
    }

    [Fact]
    public void Validate_OnlyCount_ReturnsNull()
    {
        Assert.Null(FetchSelectionResolver.Validate(count: 100, since: null));
    }

    [Fact]
    public void Validate_Neither_ReturnsNull()
    {
        Assert.Null(FetchSelectionResolver.Validate(count: null, since: null));
    }

    [Fact]
    public void Validate_InvalidSince_ReturnsError()
    {
        Assert.NotNull(FetchSelectionResolver.Validate(count: null, since: "nonsense"));
    }

    [Fact]
    public void Validate_ZeroCount_ReturnsError()
    {
        var error = FetchSelectionResolver.Validate(count: 0, since: null);
        Assert.Contains("greater than 0", error);
    }

    [Fact]
    public void Validate_MinusOneCount_ReturnsNull()
    {
        Assert.Null(FetchSelectionResolver.Validate(count: -1, since: null));
    }

    [Fact]
    public void Resolve_Since_ReturnsBySentSinceWithCutoff()
    {
        var selection = FetchSelectionResolver.Resolve(count: null, since: "30d", now: Now);
        var bySince = Assert.IsType<FetchSelection.BySentSince>(selection);
        Assert.Equal(Now.AddDays(-30), bySince.Cutoff);
    }

    [Fact]
    public void Resolve_Count_ReturnsByCount()
    {
        var selection = FetchSelectionResolver.Resolve(count: 250, since: null, now: Now);
        var byCount = Assert.IsType<FetchSelection.ByCount>(selection);
        Assert.Equal(250, byCount.Count);
    }

    [Fact]
    public void Resolve_Neither_DefaultsToThirtyDays()
    {
        var selection = FetchSelectionResolver.Resolve(count: null, since: null, now: Now);
        var bySince = Assert.IsType<FetchSelection.BySentSince>(selection);
        Assert.Equal(Now.AddDays(-30), bySince.Cutoff);
    }
}
