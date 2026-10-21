namespace EmailSweepAutomation;

/// <summary>
/// Resolves the IMAP password by precedence: explicit flag, then the MASSI_IMAP_PASSWORD
/// environment variable (which a loaded .env file populates), then an interactive prompt.
/// </summary>
public static class PasswordResolver
{
    public const string EnvVarName = "MASSI_IMAP_PASSWORD";

    public static string Resolve(string? flag, Func<string, string?> env, Func<string> prompt)
    {
        if (!string.IsNullOrEmpty(flag))
            return flag;
        var fromEnv = env(EnvVarName);
        if (!string.IsNullOrEmpty(fromEnv))
            return fromEnv;
        return prompt();
    }
}
