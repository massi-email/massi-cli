using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace EmailSweepAutomation.Commands;

/// <summary>
/// Connection flags shared by every command that opens a mailbox. Derived command settings
/// add their own members and call <c>base.Validate()</c> first.
/// </summary>
public class MailboxCommandSettings : CommandSettings
{
    [CommandArgument(0, "[Email]")]
    public string Email { get; set; } = string.Empty;

    [CommandOption("--provider <NAME>")]
    [Description("Mail provider: gmail (default), imap (custom --host), apple, yahoo, fastmail.")]
    public string Provider { get; set; } = string.Empty;

    [CommandOption("--host <HOST>")]
    public string? Host { get; set; }

    [CommandOption("--port <PORT>")]
    public int? Port { get; set; }

    [CommandOption("--starttls")]
    [Description("Use STARTTLS instead of SSL-on-connect (e.g. port 143).")]
    public bool StartTls { get; set; }

    [CommandOption("--user <USERNAME>")]
    [Description("IMAP username. Defaults to the Email argument.")]
    public string? User { get; set; }

    [CommandOption("--password <PASSWORD>")]
    [Description("IMAP password. Prefer the MASSI_IMAP_PASSWORD env var / .env file or the interactive prompt.")]
    public string? Password { get; set; }

    public override ValidationResult Validate()
    {
        var bindError = MailboxEnvBinder.Apply(this, Environment.GetEnvironmentVariable);
        if (bindError is not null)
            return ValidationResult.Error(bindError);

        var error = MailboxConnectionResolver.Validate(this);
        return error is null ? ValidationResult.Success() : ValidationResult.Error(error);
    }
}
