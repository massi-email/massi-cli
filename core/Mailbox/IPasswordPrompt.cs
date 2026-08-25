namespace EmailSweepAutomation;

/// <summary>Masked interactive prompt for an IMAP password (live console I/O seam).</summary>
public interface IPasswordPrompt
{
    string PromptSecret(string username, string host);
}
