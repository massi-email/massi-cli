namespace EmailSweepAutomation.Test.UnitTests;

using System.Reflection;
using Xunit;

/// <summary>
/// What `massi --version` says, and where the parts come from.
///
/// The published repo is source, not binaries, so a released mirror's only claim to identity
/// is what the build stamps into it. That identity has to survive being read back out of an
/// arbitrary assembly — including one built by someone who cloned the public repo and whose
/// build never saw a counter at all.
/// </summary>
public class BuildInfoTests
{
    [Fact]
    public void Parse_AStampedVersion_SplitsOutBuildAndCommit()
    {
        var info = BuildInfo.Parse("0.1.0+build.42.sha.f4dc345");

        Assert.Equal("0.1.0", info.Version);
        Assert.Equal("42", info.Build);
        Assert.Equal("f4dc345", info.Commit);
    }

    [Fact]
    public void Display_AStampedVersion_NamesTheBuildAndCommit()
    {
        var display = BuildInfo.Parse("0.1.0+build.42.sha.f4dc345").Display;

        Assert.Equal("0.1.0 (build 42, f4dc345)", display);
    }

    [Fact]
    public void Display_AnUnstampedVersion_SaysItIsALocalBuild()
    {
        // What a contributor building the public repo sees: a real version, and no false
        // claim to a build number the release process never gave it.
        var info = BuildInfo.Parse("0.1.0");

        Assert.Equal("0.1.0", info.Version);
        Assert.Null(info.Build);
        Assert.Equal("0.1.0 (local build)", info.Display);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("+build.7")]
    [InlineData("0.1.0+nonsense")]
    [InlineData("0.1.0+build.")]
    public void Parse_GarbageOrMissingMetadata_StillProducesSomethingPrintable(string? raw)
    {
        // A version string is never worth crashing a CLI over.
        var info = BuildInfo.Parse(raw);

        Assert.False(string.IsNullOrWhiteSpace(info.Display));
    }

    [Fact]
    public void ForAssembly_ReadsTheVersionTheBuildStamped()
    {
        // The end-to-end claim: whatever Directory.Build.props set is what comes back out.
        var info = BuildInfo.ForAssembly(typeof(RuleEngine).Assembly);

        Assert.False(string.IsNullOrWhiteSpace(info.Version));
        Assert.StartsWith(info.Version, info.Display);
    }

    [Fact]
    public void ForAssembly_TheEngineCarriesTheDeclaredReleaseVersion()
    {
        // Pins the version to the one place it is declared. If this fails, Directory.Build.props
        // and this expectation have drifted — and the publish gate would reject the tag too.
        var info = BuildInfo.ForAssembly(typeof(RuleEngine).Assembly);

        Assert.Equal("0.2.1", info.Version);
    }

    [Fact]
    public void ForAssembly_InformationalVersion_IsOnlyEverBuildInfosOwnFormat()
    {
        // The SDK appends "+<SourceRevisionId>" to InformationalVersion unless told not to, and
        // it lands *after* the release stamp — turning "0.1.0+build.42.sha.abc123" into a
        // longer string that Parse correctly refuses to read, so a released binary reports
        // itself as a local build. Asserting the exact shape of the raw attribute rather than
        // Parse's output is the point: Parse tolerates the corruption, which is exactly how it
        // hides. Both shapes are legal here because both are real — bare in the monorepo, where
        // no build.props exists, and stamped in the exported tree, which is the configuration
        // the publish script's standalone test run exercises.
        var raw = typeof(RuleEngine).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.NotNull(raw);
        Assert.Matches(@"^\d+\.\d+\.\d+(\+build\.\d+\.sha\.[0-9a-f]+)?$", raw);
    }

    // Right shape, wrong contents. A published tree's build.props is an ordinary file anyone
    // can edit, so "looks like a stamp" is not the same as "is a stamp" — and the whole point
    // of the counter is auditability. These are the shapes Directory.Build.props can actually
    // produce; anything else is corruption, and claiming it is worse than admitting there is
    // no stamp. Kept in step with the regex in
    // ForAssembly_InformationalVersion_IsOnlyEverBuildInfosOwnFormat.
    [Theory]
    [InlineData("0.1.0+build.x.sha.f4dc345")]      // counter not a number
    [InlineData("0.1.0+build.-1.sha.f4dc345")]     // counter negative
    [InlineData("0.1.0+build.42.sha.zzzzzz")]      // commit not hex
    [InlineData("0.1.0+build.42.sha.F4DC345")]     // commit not lowercase hex
    public void Parse_RightShapeButCorruptMetadata_ReportsALocalBuild(string raw)
    {
        var info = BuildInfo.Parse(raw);

        Assert.Equal("0.1.0", info.Version);
        Assert.Null(info.Build);
        Assert.Null(info.Commit);
        Assert.Equal("0.1.0 (local build)", info.Display);
    }
}
