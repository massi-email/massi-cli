using Spectre.Console;
using Spectre.Console.Cli;

namespace EmailSweepAutomation.Commands;

/// <summary>
/// Connection test for any provider: resolve, connect, disconnect. For Gmail this also primes
/// the cached OAuth token (like <c>gmail-login</c>); IMAP connects fresh with basic auth.
/// </summary>
public class LoginCommand : AsyncCommand<LoginCommand.Settings>
{
    private readonly IMailClientFactory mailClientFactory;
    private readonly IPasswordPrompt passwordPrompt;

    public LoginCommand(IMailClientFactory mailClientFactory, IPasswordPrompt passwordPrompt)
    {
        this.mailClientFactory = mailClientFactory ?? throw new ArgumentNullException(nameof(mailClientFactory));
        this.passwordPrompt = passwordPrompt ?? throw new ArgumentNullException(nameof(passwordPrompt));
    }

    protected async override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLineInterpolated($"Executing command [green]{nameof(LoginCommand)}[/]");
        try
        {
            var request = MailboxConnectionResolver.Resolve(settings, null);
            if (request.Provider == "imap")
            {
                var password = PasswordResolver.Resolve(
                    settings.Password,
                    Environment.GetEnvironmentVariable,
                    () => passwordPrompt.PromptSecret(request.Username ?? string.Empty, request.Host ?? string.Empty));
                request = request with { Password = password };
            }

            using var client = await mailClientFactory.ConnectAsync(request, cancellationToken);
            await client.DisconnectAsync(true);
            AnsiConsole.MarkupLineInterpolated($"[green]Authentication successful[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    public class Settings : MailboxCommandSettings
    {
    }
}
