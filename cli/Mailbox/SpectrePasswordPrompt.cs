using Spectre.Console;

namespace EmailSweepAutomation;

/// <summary>Masked console prompt (untested live-I/O shell).</summary>
public sealed class SpectrePasswordPrompt : IPasswordPrompt
{
    public string PromptSecret(string username, string host)
        => AnsiConsole.Prompt(
            new TextPrompt<string>($"Password for [green]{Markup.Escape(username)}[/] on [green]{Markup.Escape(host)}[/]:")
                .Secret());
}
