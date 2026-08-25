namespace EmailSweepAutomation;

using System.Reflection;

/// <summary>
/// Which build of Massi this is: the release version, and — for a build produced by the
/// release pipeline — the counter and upstream commit that produced it.
/// </summary>
/// <remarks>
/// The open-source repository is a one-way mirror of a monorepo and publishes source rather
/// than binaries, so "which build am I running" cannot be answered by a package version. It is
/// answered by what the build stamped into the assembly: <c>Directory.Build.props</c> sets
/// <c>InformationalVersion</c> to <c>VERSION+build.N.sha.COMMIT</c> when the release pipeline
/// supplies the two, and leaves it as the bare version otherwise. Reading it back is what makes
/// a published mirror traceable to the commit it came from — the point of shipping the engine
/// as source at all.
/// </remarks>
public sealed record BuildInfo(string Version, string? Build, string? Commit)
{
    private const string Unknown = "unknown";

    /// <summary>Printable identity, e.g. <c>0.1.0 (build 42, f4dc345)</c>.</summary>
    public string Display => Build is null || Commit is null
        ? $"{Version} (local build)"
        : $"{Version} (build {Build}, {Commit})";

    /// <summary>
    /// Reads the version an assembly was built with. Never throws: an unstamped or malformed
    /// value degrades to something printable, because no version string is worth failing a
    /// command over.
    /// </summary>
    public static BuildInfo ForAssembly(Assembly assembly) =>
        Parse(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>The engine's own build — the assembly that actually touches mail.</summary>
    public static BuildInfo Current => ForAssembly(typeof(BuildInfo).Assembly);

    /// <summary>
    /// Splits <c>VERSION+build.N.sha.COMMIT</c>. Anything that does not match that shape yields
    /// the version alone, which is the honest answer for a build nobody stamped.
    /// </summary>
    public static BuildInfo Parse(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
            return new BuildInfo(Unknown, null, null);

        var parts = informationalVersion.Split('+', 2);
        var version = parts[0].Trim();
        if (version.Length == 0)
            version = Unknown;

        if (parts.Length == 1)
            return new BuildInfo(version, null, null);

        // build.<counter>.sha.<commit> — all four segments or none. A partial stamp is a broken
        // stamp, and claiming half of one is worse than admitting there isn't one.
        var metadata = parts[1].Split('.');
        if (metadata.Length != 4 || metadata[0] != "build" || metadata[2] != "sha")
            return new BuildInfo(version, null, null);

        // Shape alone is not enough. build.props is an ordinary file in a public repository, so
        // a stamp that merely *looks* right proves nothing — and a version string that claims a
        // build number nobody issued is worse than one that admits it has none. These are exactly
        // the shapes Directory.Build.props can produce.
        var build = metadata[1];
        var commit = metadata[3];
        if (!IsCounter(build) || !IsCommit(commit))
            return new BuildInfo(version, null, null);

        return new BuildInfo(version, build, commit);
    }

    private static bool IsCounter(string value) =>
        value.Length > 0 && value.All(char.IsAsciiDigit);

    private static bool IsCommit(string value) =>
        value.Length > 0 && value.All(c => char.IsAsciiDigit(c) || (c >= 'a' && c <= 'f'));
}
